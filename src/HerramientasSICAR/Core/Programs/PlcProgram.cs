#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Programa de un PLC leído de su carpeta de exportación: bloques, símbolos, OM, secuencias, mensajes y
// referencias cruzadas, más lo que conviene avisar al usuario.
using System.Xml;
using System.Xml.Linq;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Sicar;
using HerramientasSICAR.Core.SimaticMl;
using HerramientasSICAR.Core.Symbols;

namespace HerramientasSICAR.Core.Programs;

// Escritura de una señal; Through es la variable solapada por la que se escribe, si no es por su nombre.
public sealed record SignalSource(SignalWrite Write, PlcTag? Through);

public sealed class PlcProgram
{
    private const int OperationModeBlock = 983;
    private const int OperationModeWordBytes = 2;

    private PlcProgram(string folder, string editingLanguage, IReadOnlyList<PlcBlock> blocks, SymbolTable symbols)
    {
        Folder = folder;
        EditingLanguage = editingLanguage;
        Blocks = blocks;
        Symbols = symbols;
        CrossReferences = new CrossReferences(blocks);
        Definitions = SignalDefinitions.Build(blocks);
    }

    public string Folder { get; }
    public string EditingLanguage { get; }
    public IReadOnlyList<PlcBlock> Blocks { get; }
    public SymbolTable Symbols { get; }
    public CrossReferences CrossReferences { get; }
    public IReadOnlyDictionary<string, LadExpression> Definitions { get; }
    public IReadOnlyList<OperationModeRange> OperationModeRanges { get; private init; } = [];
    public IReadOnlyList<SequenceBlock> Sequences { get; private init; } = [];
    public IReadOnlyList<MessageBlock> MessageBlocks { get; private init; } = [];
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    // Idiomas que aparecen en los nombres de paso y de mensaje (espanol, english…).
    public IReadOnlyList<string> TextLanguages { get; private init; } = [];

    // Quién escribe una señal: por su nombre o, si nadie la escribe así, a través de la variable que comparte su
    // memoria (un bit que forma parte de una doble palabra que escribe un bloque de control). Las señales de una OM
    // las escribe el FC995, que está cerrado: la cadena termina en su llamada del FC983.
    public IReadOnlyList<SignalSource> WritersOf(Operand operand)
    {
        var direct = CrossReferences.WritersOf(operand);
        if (direct.Count > 0)
        {
            return direct.Select(w => new SignalSource(w, null)).ToList();
        }
        var overlapped = Symbols.Overlapping(operand)
            .SelectMany(tag => CrossReferences.WritersOf(new Operand(OperandScope.Global, [tag.Name], "\"" + tag.Name + "\""))
                .Select(w => new SignalSource(w, tag)))
            .ToList();
        return overlapped.Count > 0 ? overlapped : OperationModeSources(operand);
    }

    private List<SignalSource> OperationModeSources(Operand operand)
    {
        var fc983 = Blocks.FirstOrDefault(b => b.Kind == BlockKind.FC && b.Number == OperationModeBlock);
        var sources = new List<SignalSource>();
        foreach (var range in OperationModeRanges)
        {
            if (fc983 != null && range.MarkerWord is { } word && Symbols.IsInMarkers(operand, word, OperationModeWordBytes) &&
                OperationModes.WriteOf(fc983, range, operand) is { } write)
            {
                sources.Add(new SignalSource(write, new PlcTag($"OM{range.Number}", "Word", $"%MW{word}")));
            }
        }
        return sources;
    }

    public int? OperationModeOf(int sequenceOrMessageBlock) => OperationModes.Of(OperationModeRanges, sequenceOrMessageBlock);

    // Analizador de doble sincronización de este programa, con las decisiones del usuario.
    public SyncChecker SyncChecker(IReadOnlyList<ExclusiveGroup> groups) =>
        new() { Definitions = Definitions, ExclusiveGroups = groups };

    // Lee todos los XML de la carpeta y sus subcarpetas. Lanza InvalidDataException si no hay programa.
    public static PlcProgram Load(string folder, string editingLanguage)
    {
        var tags = new List<PlcTag>();
        var constants = new List<PlcConstant>();
        var types = new List<PlcType>();
        var blocks = new List<PlcBlock>();
        var warnings = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                var document = XDocument.Load(file);
                if (SymbolReader.IsTagTable(document))
                {
                    var (fileTags, fileConstants) = SymbolReader.ReadTagTable(document);
                    tags.AddRange(fileTags);
                    constants.AddRange(fileConstants);
                }
                else if (SymbolReader.IsType(document))
                {
                    types.Add(SymbolReader.ReadType(document));
                }
                else
                {
                    blocks.Add(BlockReader.Read(document));
                }
            }
            catch (Exception e) when (e is XmlException or InvalidDataException)
            {
                warnings.Add(L.T("No se pudo leer {0}: {1}", Path.GetFileName(file), e.Message));
            }
        }

        if (blocks.Count == 0)
        {
            throw new InvalidDataException(L.T("La carpeta no tiene bloques exportados: {0}", folder));
        }

        var sequences = blocks.Where(SicarReader.IsSequenceBlock)
            .Select(b => SicarReader.ReadSequence(b, editingLanguage))
            .OrderBy(s => s.Sequence)
            .ToList();
        var messages = blocks.Where(SicarReader.IsMessageBlock)
            .Select(b => SicarReader.ReadMessages(b, editingLanguage))
            .OrderBy(m => m.Number)
            .ToList();

        var fc983 = blocks.FirstOrDefault(b => b.Kind == BlockKind.FC && b.Number == OperationModeBlock);
        var ranges = fc983 == null ? [] : OperationModes.Read(fc983);
        if (ranges.Count == 0)
        {
            warnings.Add(L.T("No se han encontrado las OM en el FC983: las secuencias salen sin zona operacional."));
        }

        foreach (var block in blocks)
        {
            warnings.AddRange(block.Networks.Where(n => n.Warning != null)
                .Select(n => L.T("{0}, red {1}: {2}", block.Name, n.Index, n.Warning)));
        }
        warnings.AddRange(sequences.Where(s => s.Permanent == null)
            .Select(s => L.T("La secuencia {0} no tiene paso PERM.", s.Sequence)));

        var steps = sequences.SelectMany(s => s.Steps).ToList();
        if (steps.Count > 0 && steps.All(s => s.Titles.Count == 0))
        {
            warnings.Add(L.T("Ningún paso tiene nombre en el idioma de edición {0}. Comprueba el idioma de edición del PLC.", editingLanguage));
        }

        var languages = steps.SelectMany(s => s.Titles.Keys)
            .Concat(messages.SelectMany(m => m.Messages).SelectMany(m => m.Titles.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PlcProgram(folder, editingLanguage, blocks, new SymbolTable(tags, constants, types))
        {
            OperationModeRanges = ranges,
            Sequences = sequences,
            MessageBlocks = messages,
            Warnings = warnings,
            TextLanguages = languages,
        };
    }
}
