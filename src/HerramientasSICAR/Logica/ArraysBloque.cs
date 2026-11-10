using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HerramientasSICAR.Logica
{
    /// <summary>Un array que el código de un bloque usa con índice fijo.</summary>
    public sealed class ArrayDelBloque
    {
        public ArrayDelBloque(string nombre, IReadOnlyList<long> indices)
        {
            Nombre = nombre;
            Indices = indices;
        }

        public string Nombre { get; }

        /// <summary>Índices de cada uso, en el orden en que aparecen en el bloque.</summary>
        public IReadOnlyList<long> Indices { get; }

        public int Usos => Indices.Count;
    }

    /// <summary>
    /// Renumerar Array: los arrays que el código de un bloque (XML de Openness) usa con un índice fijo
    /// (<c>miArray[3]</c>) y su renumeración 1, 2, 3… en el orden en que aparecen. Los usos con índice variable
    /// (<c>miArray[#i]</c>) no se cuentan ni se tocan. No depende de TIA Portal.
    /// </summary>
    public static class ArraysBloque
    {
        /// <summary>Arrays con índice fijo del bloque, en el orden en que aparece el primer uso de cada uno.</summary>
        public static IReadOnlyList<ArrayDelBloque> Listar(string xml)
        {
            var indices = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
            var orden = new List<string>();
            foreach (var (nombre, valor) in UsosConIndiceFijo(XDocument.Parse(xml)))
            {
                if (!indices.TryGetValue(nombre, out var lista))
                {
                    indices[nombre] = lista = new List<long>();
                    orden.Add(nombre);
                }
                lista.Add(long.TryParse(valor.Value, out long n) ? n : 0);
            }
            return orden.Select(n => new ArrayDelBloque(n, indices[n])).ToList();
        }

        /// <summary>
        /// Pone a cada uso con índice fijo del array (sin distinguir mayúsculas) los índices 1, 2, 3… en el orden en
        /// que aparecen. Devuelve el XML nuevo; el resto del bloque queda igual.
        /// </summary>
        public static string Renumerar(string xml, string nombreArray, out int cambios)
        {
            var documento = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            int contador = 0;
            foreach (var (nombre, valor) in UsosConIndiceFijo(documento))
                if (nombre.Equals(nombreArray, StringComparison.OrdinalIgnoreCase))
                    valor.Value = (++contador).ToString();
            if (contador == 0)
                throw new InvalidOperationException(L.T("No se encontró el array '{0}' en el bloque.", nombreArray));

            cambios = contador;
            var declaracion = documento.Declaration != null ? documento.Declaration + "\n" : "";
            return declaracion + documento.Root.ToString(SaveOptions.DisableFormatting);
        }

        // Cada acceso a un elemento de array cuyo único índice es una constante: <Component Name="x" AccessModifier="Array">
        // con un <Access Scope="LiteralConstant"> dentro. Se devuelve el nombre y el nodo ConstantValue.
        private static IEnumerable<(string Nombre, XElement Valor)> UsosConIndiceFijo(XDocument documento)
        {
            foreach (var componente in documento.Descendants().Where(e => e.Name.LocalName == "Component"
                         && (string)e.Attribute("AccessModifier") == "Array"))
            {
                var indices = componente.Elements().Where(e => e.Name.LocalName == "Access").ToList();
                if (indices.Count != 1 || (string)indices[0].Attribute("Scope") != "LiteralConstant")
                    continue;
                var valor = indices[0].Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
                if (valor != null)
                    yield return ((string)componente.Attribute("Name"), valor);
            }
        }
    }
}
