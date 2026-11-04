#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Ayudas para navegar SimaticML sin depender de los espacios de nombres de cada versión de TIA.
using System.Xml.Linq;

namespace HerramientasSICAR.Nucleo.SimaticMl;

internal static class XmlExtensions
{
    public static XElement? Child(this XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    // Valor de un atributo de la AttributeList de un objeto (Number, ProgrammingLanguage, Culture…).
    public static string? Attribute(this XElement element, string name, bool fromAttributeList) =>
        fromAttributeList ? element.Child("AttributeList")?.Child(name)?.Value : (string?)element.Attribute(name);
}
