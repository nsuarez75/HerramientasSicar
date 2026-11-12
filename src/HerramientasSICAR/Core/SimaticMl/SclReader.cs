#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Lee una red en SCL (StructuredText): su texto, para mostrarlo, y qué variables escribe. No se analizan sus
// condiciones: una escritura en SCL se ve como «se escribe aquí» y la cadena para ahí.
using System.Text;
using System.Xml.Linq;
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.SimaticMl;

internal static class SclReader
{
    public static (string Text, IReadOnlyList<Assignment> Writes) Read(XElement structuredText)
    {
        var text = new StringBuilder();
        var writes = new List<Assignment>();
        var parts = structuredText.Elements().ToList();
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            switch (part.Name.LocalName)
            {
                case "Access":
                    var operand = OperandReader.Read(part);
                    text.Append(operand.Text);
                    if (NextIsAssignment(parts, i))
                    {
                        writes.Add(new Assignment(AssignmentKind.BoxOutput, operand, new UnknownExpression("SCL")) { Box = "SCL", Conditional = true });
                    }
                    break;
                case "Token":
                    text.Append((string?)part.Attribute("Text"));
                    break;
                case "Text":
                    text.Append(part.Value);
                    break;
                case "Blank":
                    text.Append(' ', Count(part));
                    break;
                case "NewLine":
                    text.Append('\n', Count(part));
                    break;
                case "LineComment":
                    text.Append("//").Append(part.Value);
                    break;
            }
        }
        return (text.ToString(), writes);
    }

    private static bool NextIsAssignment(List<XElement> parts, int index)
    {
        for (var i = index + 1; i < parts.Count; i++)
        {
            switch (parts[i].Name.LocalName)
            {
                case "Blank" or "NewLine":
                    continue;
                case "Token":
                    return (string?)parts[i].Attribute("Text") == ":=";
                default:
                    return false;
            }
        }
        return false;
    }

    private static int Count(XElement part) => int.TryParse((string?)part.Attribute("Num"), out var n) ? n : 1;
}
