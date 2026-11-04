#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Lee un bloque de programa exportado de TIA (SimaticML) con sus redes.
using System.Xml.Linq;
using HerramientasSICAR.Nucleo.Lad;

namespace HerramientasSICAR.Nucleo.SimaticMl;

public static class BlockReader
{
    public static PlcBlock Read(string path) => Read(XDocument.Load(path));

    public static PlcBlock Read(XDocument document)
    {
        var block = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal))
            ?? throw new InvalidDataException("El archivo no es un bloque de programa exportado de TIA.");

        var language = block.Attribute("ProgrammingLanguage", fromAttributeList: true) ?? "";
        var networks = new List<Network>();
        var index = 0;
        foreach (var unit in block.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit"))
        {
            index++;
            networks.Add(ReadNetwork(unit, index, language));
        }

        return new PlcBlock(
            KindOf(block.Name.LocalName),
            int.TryParse(block.Attribute("Number", fromAttributeList: true), out var number) ? number : 0,
            block.Attribute("Name", fromAttributeList: true) ?? "",
            language)
        {
            Networks = networks,
            Title = Text(block, "Title"),
            InstanceOf = block.Attribute("InstanceOfName", fromAttributeList: true),
        };
    }

    private static Network ReadNetwork(XElement unit, int index, string blockLanguage)
    {
        // Una red sin lenguaje propio hereda el del bloque.
        var language = unit.Attribute("ProgrammingLanguage", fromAttributeList: true) ?? blockLanguage;
        var network = new Network(index, language)
        {
            Title = Text(unit, "Title"),
            Comment = Text(unit, "Comment"),
        };

        if (!network.IsLad)
        {
            return ReadText(unit, network);
        }

        var flgNet = unit.Descendants().FirstOrDefault(e => e.Name.LocalName == "FlgNet");
        if (flgNet == null)
        {
            return network;
        }

        try
        {
            var parser = new LadNetworkParser(flgNet);
            return network with { Label = parser.Label, Assignments = parser.Assignments().ToList(), Calls = parser.Calls().ToList() };
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or NullReferenceException)
        {
            return network with { Warning = "No se ha podido interpretar la red: " + e.Message };
        }
    }

    // Redes en STL y SCL: lo que escriben y su texto.
    private static Network ReadText(XElement unit, Network network)
    {
        var statements = unit.Descendants().FirstOrDefault(e => e.Name.LocalName == "StatementList");
        if (statements != null)
        {
            var parser = new StlNetworkParser(statements);
            return network with { Label = parser.Label, Assignments = parser.Assignments, Calls = parser.Calls, Lines = parser.Lines };
        }
        var text = unit.Descendants().FirstOrDefault(e => e.Name.LocalName == "StructuredText");
        if (text != null)
        {
            var (source, writes) = SclReader.Read(text);
            return network with { Source = source, Assignments = writes };
        }
        return network;
    }

    // Textos multilingües (Title, Comment) de un objeto: cultura → texto.
    private static MultilingualText Text(XElement owner, string composition)
    {
        var text = owner.Child("ObjectList")?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string?)e.Attribute("CompositionName") == composition);
        if (text == null)
        {
            return MultilingualText.Empty;
        }
        var items = new Dictionary<string, string>();
        foreach (var item in text.Descendants().Where(e => e.Name.LocalName == "MultilingualTextItem"))
        {
            var culture = item.Attribute("Culture", fromAttributeList: true);
            if (culture is { Length: > 0 })
            {
                items[culture] = (item.Attribute("Text", fromAttributeList: true) ?? "").Trim();
            }
        }
        return new MultilingualText(items);
    }

    private static BlockKind KindOf(string element) => element switch
    {
        "SW.Blocks.FB" => BlockKind.FB,
        "SW.Blocks.FC" => BlockKind.FC,
        "SW.Blocks.OB" => BlockKind.OB,
        "SW.Blocks.GlobalDB" => BlockKind.GlobalDB,
        "SW.Blocks.InstanceDB" => BlockKind.InstanceDB,
        _ => BlockKind.Other,
    };
}
