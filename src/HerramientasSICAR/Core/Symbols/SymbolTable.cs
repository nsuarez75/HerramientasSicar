#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Tabla de símbolos del PLC: de cada operando global se sabe su tipo, su comentario y, si es una marca o una
// entrada, su dirección (las de los miembros de variables de tipo UDT se calculan). Las constantes de usuario
// dan el valor de los operandos «"NOMBRE"» de los comparadores.
using System.Globalization;
using System.Text.RegularExpressions;
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Symbols;

public sealed partial class SymbolTable
{
    private readonly Dictionary<string, SymbolInfo> _symbols = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlcConstant> _constants = new(StringComparer.Ordinal);
    // Zona de memoria de cada variable, para saber cuáles se solapan (un bit dentro de una doble palabra).
    private readonly List<(PlcTag Tag, char Area, int First, int Last)> _ranges = [];
    private readonly Dictionary<string, (char Area, int First, int Last)> _rangeByName = new(StringComparer.Ordinal);

    public SymbolTable(IEnumerable<PlcTag> tags, IEnumerable<PlcConstant> constants, IEnumerable<PlcType> types)
    {
        var typeMap = types.GroupBy(t => t.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var constant in constants)
        {
            _constants[constant.Name] = constant;
        }
        foreach (var tag in tags)
        {
            var area = Area(tag.Address);
            if (Range(tag.Address, tag.DataType, typeMap) is { } range)
            {
                _ranges.Add((tag, area, range.First, range.Last));
                _rangeByName[tag.Name] = (area, range.First, range.Last);
            }
            var showAddress = area is 'I' or 'M';
            _symbols[Key([tag.Name])] = new SymbolInfo(tag.Name, tag.DataType)
            {
                Comment = tag.Comment,
                Address = showAddress ? tag.Address : null,
            };

            if (!typeMap.TryGetValue(tag.DataType, out var udt))
            {
                continue;
            }
            var startBit = BitAddress(tag.Address);
            var offsets = showAddress && startBit != null ? MemoryLayout.Offsets(udt.Members, typeMap) : null;
            AddMembers(tag.Name, udt.Members, typeMap, offsets, area, startBit ?? 0, prefix: "", depth: 0);
        }
    }

    public int Count => _symbols.Count;

    // Información de un operando global: "Señal", "Variable".Miembro…
    public SymbolInfo? Find(Operand operand) =>
        operand.Scope == OperandScope.Global && _symbols.TryGetValue(Key(operand.Components), out var info) ? info : null;

    // Variables de la tabla que comparten memoria con esta: la doble palabra que contiene un bit, los bits de una
    // palabra… Solo para variables de la tabla, no para sus miembros.
    public IReadOnlyList<PlcTag> Overlapping(Operand operand)
    {
        if (operand.Scope != OperandScope.Global || operand.Components.Count != 1 ||
            !_rangeByName.TryGetValue(operand.Components[0], out var range))
        {
            return [];
        }
        return _ranges
            .Where(r => r.Area == range.Area && r.First <= range.Last && r.Last >= range.First && r.Tag.Name != operand.Components[0])
            .Select(r => r.Tag)
            .ToList();
    }

    // Si una variable de la tabla está dentro de unos bytes de marcas (la palabra de una OM).
    public bool IsInMarkers(Operand operand, int firstByte, int bytes) =>
        operand.Scope == OperandScope.Global && operand.Components.Count == 1 &&
        _rangeByName.TryGetValue(operand.Components[0], out var range) && range.Area == 'M' &&
        range.First >= firstByte * 8 && range.Last < (firstByte + bytes) * 8;

    public PlcConstant? Constant(string name) => _constants.GetValueOrDefault(name.Trim('"'));

    // Valor de una constante de usuario ya interpretado (número, booleano…).
    public object? ConstantValue(Operand operand) =>
        operand.Scope == OperandScope.GlobalConstant && Constant(operand.LastName) is { } c ? Diagnostics.Literals.Parse(c.Value) : null;

    private void AddMembers(string root, IReadOnlyList<TypeMember> members, IReadOnlyDictionary<string, PlcType> types,
        IReadOnlyDictionary<string, int>? offsets, char area, int start, string prefix, int depth)
    {
        if (depth > 10)
        {
            return;
        }
        foreach (var member in members)
        {
            var path = prefix + member.Name;
            string? address = null;
            if (offsets != null && offsets.TryGetValue(path, out var bit))
            {
                var absolute = start + bit;
                address = $"%{area}{absolute / 8}.{absolute % 8}";
            }
            _symbols[Key([root, .. path.Split('.')])] = new SymbolInfo(root + "." + path, member.DataType)
            {
                Comment = member.Comment,
                Address = address,
            };
            var nested = member.Members.Count > 0 ? member.Members : types.TryGetValue(member.DataType, out var udt) ? udt.Members : [];
            if (nested.Count > 0)
            {
                AddMembers(root, nested, types, offsets, area, start, path + ".", depth + 1);
            }
        }
    }

    // Bits que ocupa una variable en su zona: %M10.3 Bool, %MD100 DWord, %I100.0 de tipo UDT…
    private static (int First, int Last)? Range(string address, string dataType, IReadOnlyDictionary<string, PlcType> types)
    {
        var match = AnyAddressPattern().Match(address);
        if (!match.Success)
        {
            return null;
        }
        var first = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) * 8 +
                    (match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0);
        var size = match.Groups[2].Value.ToUpperInvariant() switch
        {
            "B" => 8,
            "W" => 16,
            "D" => 32,
            _ => MemoryLayout.SizeOf(dataType, types),
        };
        return (first, first + Math.Max(size, 1) - 1);
    }

    private static string Key(IEnumerable<string> components) => string.Join("\u001F", components);

    private static char Area(string address)
    {
        var match = AnyAddressPattern().Match(address);
        return match.Success ? char.ToUpperInvariant(match.Groups[1].Value[0]) : '?';
    }

    // Dirección de bit de «%I10000.0» o «%M10.3»; null si no es de bit.
    private static int? BitAddress(string address)
    {
        var match = AddressPattern().Match(address);
        if (!match.Success || !match.Groups[3].Success)
        {
            return null;
        }
        return int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 8 + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    }

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex AddressPattern() => _AddressPattern ??= new Regex(@"^%([IQM])(\d+)(?:\.(\d))?$", RegexOptions.IgnoreCase);

    private static Regex? _AddressPattern;

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex AnyAddressPattern() => _AnyAddressPattern ??= new Regex(@"^%([IQM])([BWD]?)(\d+)(?:\.(\d))?$", RegexOptions.IgnoreCase);

    private static Regex? _AnyAddressPattern;
}
