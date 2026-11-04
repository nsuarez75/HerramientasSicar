#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Lee de SimaticML las tablas de variables (con sus constantes) y los tipos de datos (UDT).
using System.Xml.Linq;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.SimaticMl;

namespace HerramientasSICAR.Nucleo.Symbols;

public static class SymbolReader
{
    public static bool IsTagTable(XDocument document) => Root(document)?.Name.LocalName == "SW.Tags.PlcTagTable";

    public static bool IsType(XDocument document) => Root(document)?.Name.LocalName == "SW.Types.PlcStruct";

    public static (IReadOnlyList<PlcTag> Tags, IReadOnlyList<PlcConstant> Constants) ReadTagTable(XDocument document)
    {
        var table = Root(document) ?? throw new InvalidDataException("El archivo no es una tabla de variables.");
        var tags = new List<PlcTag>();
        var constants = new List<PlcConstant>();
        foreach (var item in table.Child("ObjectList")?.Elements() ?? [])
        {
            var name = item.Attribute("Name", fromAttributeList: true) ?? "";
            var type = (item.Attribute("DataTypeName", fromAttributeList: true) ?? "").Trim('"');
            var comment = Text(item);
            switch (item.Name.LocalName)
            {
                case "SW.Tags.PlcTag":
                    tags.Add(new PlcTag(name, type, item.Attribute("LogicalAddress", fromAttributeList: true) ?? "") { Comment = comment });
                    break;
                case "SW.Tags.PlcUserConstant":
                    constants.Add(new PlcConstant(name, type, item.Attribute("Value", fromAttributeList: true) ?? "") { Comment = comment });
                    break;
            }
        }
        return (tags, constants);
    }

    public static PlcType ReadType(XDocument document)
    {
        var type = Root(document) ?? throw new InvalidDataException("El archivo no es un tipo de datos.");
        var section = type.Descendants().FirstOrDefault(e => e.Name.LocalName == "Section");
        return new PlcType(type.Attribute("Name", fromAttributeList: true) ?? "", Members(section));
    }

    // Miembros de la interfaz de un bloque o tipo (DB, FB…): sección «Static» o «None».
    public static IReadOnlyList<TypeMember> InterfaceMembers(XElement block, params string[] sections) =>
        block.Descendants()
            .Where(e => e.Name.LocalName == "Section" && sections.Contains((string?)e.Attribute("Name")))
            .SelectMany(Members)
            .ToList();

    private static List<TypeMember> Members(XElement? section) =>
        section?.Elements().Where(e => e.Name.LocalName == "Member").Select(Member).ToList() ?? [];

    private static TypeMember Member(XElement member)
    {
        var items = member.Child("Comment")?.Elements().Where(e => e.Name.LocalName == "MultiLanguageText")
            .ToDictionary(e => (string?)e.Attribute("Lang") ?? "", e => e.Value.Trim()) ?? [];
        var nested = member.Child("Sections")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Section");
        return new TypeMember((string?)member.Attribute("Name") ?? "", ((string?)member.Attribute("Datatype") ?? "").Trim('"'))
        {
            Comment = new MultilingualText(items),
            Members = Members(nested),
        };
    }

    private static MultilingualText Text(XElement owner)
    {
        var text = owner.Child("ObjectList")?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string?)e.Attribute("CompositionName") == "Comment");
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

    private static XElement? Root(XDocument document) =>
        document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.", StringComparison.Ordinal));
}
