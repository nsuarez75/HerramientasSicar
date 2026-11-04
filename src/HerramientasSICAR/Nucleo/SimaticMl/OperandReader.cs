#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Lee el elemento Access de SimaticML (o Instance) y lo convierte en un operando.
using System.Xml.Linq;
using HerramientasSICAR.Nucleo.Lad;

namespace HerramientasSICAR.Nucleo.SimaticMl;

internal static class OperandReader
{
    public static Operand Read(XElement access)
    {
        var scope = (string?)access.Attribute("Scope") ?? "";

        var symbol = access.Child("Symbol");
        if (symbol != null)
        {
            var components = symbol.Elements().Where(e => e.Name.LocalName == "Component").Select(ComponentText).ToList();
            return scope == "GlobalVariable"
                ? new Operand(OperandScope.Global, components, string.Join(".", components.Select(Quote)))
                : new Operand(OperandScope.Local, components, "#" + string.Join(".", components));
        }

        var constant = access.Child("Constant");
        if (constant != null)
        {
            var value = constant.Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
            if (value != null)
            {
                return new Operand(OperandScope.Literal, [], value.Value);
            }
            var name = (string?)constant.Attribute("Name") ?? "";
            return scope == "GlobalConstant"
                ? new Operand(OperandScope.GlobalConstant, [name], Quote(name))
                : new Operand(OperandScope.LocalConstant, [name], "#" + name);
        }

        var label = access.Child("Label");
        if (label != null)
        {
            var name = (string?)label.Attribute("Name") ?? "";
            return new Operand(OperandScope.Label, [name], name);
        }

        var address = access.Child("Address");
        if (address != null)
        {
            var text = "%" + (string?)address.Attribute("Area") + (string?)address.Attribute("BitOffset");
            return new Operand(OperandScope.Address, [], text);
        }

        return Operand.Unknown(access.Value);
    }

    // En SimaticML los nombres globales van entre comillas en TIA; los miembros con índice llevan [i].
    private static string Quote(string component)
    {
        var bracket = component.IndexOf('[');
        return bracket < 0 ? "\"" + component + "\"" : "\"" + component[..bracket] + "\"" + component[bracket..];
    }

    private static string ComponentText(XElement component)
    {
        var name = (string?)component.Attribute("Name") ?? "";
        var indexes = component.Elements().Where(e => e.Name.LocalName == "Access").Select(a => Read(a).Text).ToList();
        return indexes.Count > 0 ? name + "[" + string.Join(",", indexes) + "]" : name;
    }
}
