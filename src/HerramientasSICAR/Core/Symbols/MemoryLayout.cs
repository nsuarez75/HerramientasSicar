#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Distribución en memoria de un tipo con acceso estándar (no optimizado), como la de S7-300: los Bool se
// empaquetan bit a bit, Byte/Char/SInt/USInt van a byte, el resto de tipos, las estructuras, los UDT y los
// arrays empiezan en byte par, y las estructuras y arrays ocupan un número par de bytes. Sirve para saber
// la dirección de cada miembro de una variable de tipo UDT (p. ej. %I10000.3), solo como información.
using System.Text.RegularExpressions;

namespace HerramientasSICAR.Core.Symbols;

public static partial class MemoryLayout
{
    // Desplazamiento en bits de cada miembro hoja desde el inicio del tipo: «soAutoOn» → 3, «Datos.Nivel» → 32.
    public static IReadOnlyDictionary<string, int> Offsets(IReadOnlyList<TypeMember> members, IReadOnlyDictionary<string, PlcType> types)
    {
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var cursor = 0;
        Place(members, "", ref cursor, offsets, types, 0);
        return offsets;
    }

    // Tamaño en bits de un tipo, ya redondeado a su alineación.
    public static int SizeOf(string dataType, IReadOnlyDictionary<string, PlcType> types)
    {
        var cursor = 0;
        PlaceOne(new TypeMember("x", dataType), "", ref cursor, new Dictionary<string, int>(), types, 0);
        return cursor;
    }

    private static void Place(IReadOnlyList<TypeMember> members, string prefix, ref int cursor, Dictionary<string, int> offsets,
        IReadOnlyDictionary<string, PlcType> types, int depth)
    {
        foreach (var member in members)
        {
            PlaceOne(member, prefix, ref cursor, offsets, types, depth);
        }
    }

    private static void PlaceOne(TypeMember member, string prefix, ref int cursor, Dictionary<string, int> offsets,
        IReadOnlyDictionary<string, PlcType> types, int depth)
    {
        var path = prefix + member.Name;
        var type = member.DataType.Trim('"');

        if (depth > 20)
        {
            return;
        }

        var array = ArrayType().Match(type);
        if (array.Success)
        {
            var count = int.Parse(array.Groups[2].Value) - int.Parse(array.Groups[1].Value) + 1;
            var elementType = array.Groups[3].Value.Trim().Trim('"');
            var low = int.Parse(array.Groups[1].Value);
            AlignWord(ref cursor);
            for (var i = 0; i < count; i++)
            {
                if (elementType.Equals("Bool", StringComparison.OrdinalIgnoreCase))
                {
                    offsets[$"{path}[{low + i}]"] = cursor++;
                }
                else
                {
                    PlaceOne(new TypeMember($"{member.Name}[{low + i}]", elementType), prefix, ref cursor, offsets, types, depth + 1);
                }
            }
            AlignWord(ref cursor);
            return;
        }

        if (member.Members.Count > 0 || type.Equals("Struct", StringComparison.OrdinalIgnoreCase))
        {
            AlignWord(ref cursor);
            Place(member.Members, path + ".", ref cursor, offsets, types, depth + 1);
            AlignWord(ref cursor);
            return;
        }

        if (types.TryGetValue(type, out var udt))
        {
            AlignWord(ref cursor);
            Place(udt.Members, path + ".", ref cursor, offsets, types, depth + 1);
            AlignWord(ref cursor);
            return;
        }

        var size = ElementaryBytes(type);
        if (size == 0)
        {
            // Bool: un bit, sin alinear.
            offsets[path] = cursor++;
            return;
        }
        if (size == 1)
        {
            AlignByte(ref cursor);
        }
        else
        {
            AlignWord(ref cursor);
        }
        offsets[path] = cursor;
        cursor += size * 8;
    }

    // Bytes de un tipo elemental; 0 para Bool.
    private static int ElementaryBytes(string type)
    {
        var lower = type.ToLowerInvariant();
        var text = StringType().Match(lower);
        if (text.Success)
        {
            var length = text.Groups[2].Success ? int.Parse(text.Groups[2].Value) : 254;
            var bytes = text.Groups[1].Value == "wstring" ? 2 * length + 4 : length + 2;
            return bytes + bytes % 2;
        }
        return lower switch
        {
            "bool" => 0,
            "byte" or "char" or "sint" or "usint" => 1,
            "word" or "int" or "uint" or "wchar" or "date" or "s5time" => 2,
            "dword" or "dint" or "udint" or "real" or "time" or "time_of_day" or "tod" => 4,
            "lword" or "lint" or "ulint" or "lreal" or "ltime" or "ltod" or "ltime_of_day" or "date_and_time" or "dt" => 8,
            "dtl" => 12,
            _ => 2,
        };
    }

    private static void AlignByte(ref int cursor) => cursor = (cursor + 7) / 8 * 8;

    private static void AlignWord(ref int cursor) => cursor = (cursor + 15) / 16 * 16;

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex ArrayType() => _ArrayType ??= new Regex(@"^Array\s*\[\s*(-?\d+)\s*\.\.\s*(-?\d+)\s*\]\s*of\s+(.+)$", RegexOptions.IgnoreCase);

    private static Regex? _ArrayType;

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex StringType() => _StringType ??= new Regex(@"^(w?string)(?:\[(\d+)\])?$");

    private static Regex? _StringType;
}
