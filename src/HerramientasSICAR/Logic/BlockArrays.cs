using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HerramientasSICAR.Logic
{
    /// <summary>Un array que el código de un bloque usa con índice fijo.</summary>
    public sealed class BlockArray
    {
        public BlockArray(string name, IReadOnlyList<long> indexes)
        {
            Name = name;
            Indexes = indexes;
        }

        public string Name { get; }

        /// <summary>Índices de cada uso, en el orden en que aparecen en el bloque.</summary>
        public IReadOnlyList<long> Indexes { get; }

        public int Uses => Indexes.Count;
    }

    /// <summary>
    /// Renumerar Array: los arrays que el código de un bloque (XML de Openness) usa con un índice fijo
    /// (<c>miArray[3]</c>) y su renumeración 1, 2, 3… en el orden en que aparecen. Los usos con índice variable
    /// (<c>miArray[#i]</c>) no se cuentan ni se tocan. No depende de TIA Portal.
    /// </summary>
    public static class BlockArrays
    {
        /// <summary>Arrays con índice fijo del bloque, en el orden en que aparece el primer uso de cada uno.</summary>
        public static IReadOnlyList<BlockArray> ListArrays(string xml)
        {
            var indexes = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var (name, value) in FixedIndexUses(XDocument.Parse(xml)))
            {
                if (!indexes.TryGetValue(name, out var list))
                {
                    indexes[name] = list = new List<long>();
                    order.Add(name);
                }
                list.Add(long.TryParse(value.Value, out long n) ? n : 0);
            }
            return order.Select(n => new BlockArray(n, indexes[n])).ToList();
        }

        /// <summary>
        /// Pone a cada uso con índice fijo del array (sin distinguir mayúsculas) los índices 1, 2, 3… en el orden en
        /// que aparecen. Devuelve el XML nuevo; el resto del bloque queda igual.
        /// </summary>
        public static string Renumber(string xml, string arrayName, out int changes)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            int counter = 0;
            foreach (var (name, value) in FixedIndexUses(document))
                if (name.Equals(arrayName, StringComparison.OrdinalIgnoreCase))
                    value.Value = (++counter).ToString();
            if (counter == 0)
                throw new InvalidOperationException(L.T("No se encontró el array '{0}' en el bloque.", arrayName));

            changes = counter;
            var declaration = document.Declaration != null ? document.Declaration + "\n" : "";
            return declaration + document.Root.ToString(SaveOptions.DisableFormatting);
        }

        // Cada acceso a un elemento de array cuyo único índice es una constante: <Component Name="x" AccessModifier="Array">
        // con un <Access Scope="LiteralConstant"> dentro. Se devuelve el nombre y el nodo ConstantValue.
        private static IEnumerable<(string Name, XElement Value)> FixedIndexUses(XDocument document)
        {
            foreach (var component in document.Descendants().Where(e => e.Name.LocalName == "Component"
                         && (string)e.Attribute("AccessModifier") == "Array"))
            {
                var indexes = component.Elements().Where(e => e.Name.LocalName == "Access").ToList();
                if (indexes.Count != 1 || (string)indexes[0].Attribute("Scope") != "LiteralConstant")
                    continue;
                var value = indexes[0].Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
                if (value != null)
                    yield return ((string)component.Attribute("Name"), value);
            }
        }
    }
}
