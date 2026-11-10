using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HerramientasSICAR.Logica
{
    /// <summary>Idioma en que están escritos los títulos de los segmentos.</summary>
    public enum IdiomaTitulos { Castellano, Ingles, Aleman }

    /// <summary>Un bloque _FB de la carpeta de secuencias de un PLC.</summary>
    public sealed class FbSecuencia
    {
        public FbSecuencia(string nombre, IReadOnlyList<string> grupos, bool protegido)
        {
            Nombre = nombre;
            Grupos = grupos;
            Protegido = protegido;
        }

        public string Nombre { get; }

        /// <summary>Carpetas de TIA desde la raíz del PLC hasta la del bloque.</summary>
        public IReadOnlyList<string> Grupos { get; }

        public bool Protegido { get; }
    }

    /// <summary>Las tres líneas del comentario de un segmento.</summary>
    public sealed class TextosComentario
    {
        public TextosComentario(string ingles, string aleman, string castellano)
        {
            Ingles = ingles;
            Aleman = aleman;
            Castellano = castellano;
        }

        public string Ingles { get; }
        public string Aleman { get; }
        public string Castellano { get; }

        /// <summary>Comentario del segmento, en el formato de SICAR.</summary>
        public string Comentario() =>
            $"Title_english {Ingles}\nTitle_deutsch {Aleman}\nTitle_espanol {Castellano}";
    }

    /// <summary>
    /// Comentador: reglas para comentar los segmentos de los FB de secuencia a partir de su título, sobre el XML
    /// exportado con Openness. No depende de TIA Portal.
    /// </summary>
    public static class ComentariosSecuencia
    {
        /// <summary>Carpeta de TIA donde están los FB de secuencia y de mensajes.</summary>
        public const string CarpetaSecuencias = "10_Sequence & Messageblocks";

        /// <summary>Idioma del proyecto: de él se lee el título y en él se escribe el comentario.</summary>
        public const string Cultura = "en-US";

        private static readonly Regex Paso = new Regex(@"\bS\d{3}\b");
        private static readonly Regex Om = new Regex(@"^OM[1-8]$");

        /// <summary>Código del traductor para cada idioma de los títulos.</summary>
        public static string Codigo(IdiomaTitulos idioma) =>
            idioma == IdiomaTitulos.Ingles ? "en" : idioma == IdiomaTitulos.Aleman ? "de" : "es";

        /// <summary>Un bloque entra en la lista del Comentador si su nombre acaba en _FB.</summary>
        public static bool EsFb(string nombre) => nombre.EndsWith("_FB", StringComparison.Ordinal);

        /// <summary>
        /// Si sale marcado al listar: un FB de secuencia de una estación (bajo una carpeta OM1 a OM8), que no sea de
        /// Summary, Messages ni de la carpeta 50_Safety. Es el filtro que usaba el Comentador con el Excel.
        /// </summary>
        /// <param name="grupos">Carpetas desde la de secuencias (sin incluirla) hasta la del bloque.</param>
        public static bool MarcadoPorDefecto(IReadOnlyList<string> grupos, string nombre) =>
            EsFb(nombre)
            && grupos.Any(g => Om.IsMatch(g))
            && !grupos.Any(g => g.Contains("50_Safety"))
            && !nombre.Contains("Summary")
            && !nombre.Contains("Messages");

        // Separadores de segmentos («==== SEQUENCE ====», «------»): no se comentan.
        private static readonly Regex Separador = new Regex(@"^\s*[=\-*#]{3,}");

        /// <summary>Título sin el número de paso (S###), o null si es un separador o no queda ningún texto que comentar.</summary>
        public static string TituloSinPaso(string titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo) || Separador.IsMatch(titulo))
                return null;
            string limpio = Regex.Replace(Paso.Replace(titulo, ""), @"\s+", " ").Trim();
            return limpio.Any(char.IsLetter) ? limpio : null;
        }

        /// <summary>Los nombres de equipo (con «++») no se traducen: el traductor les cambia espacios y letras.</summary>
        public static bool EsNombreDeEquipo(string texto) => texto != null && texto.Contains("++");

        /// <summary>Títulos distintos (ya sin S###) de los segmentos que se van a comentar.</summary>
        public static IReadOnlyList<string> Titulos(string xml) =>
            Segmentos(XDocument.Parse(xml)).Select(s => TituloSinPaso(Texto(s, "Title")))
                .Where(t => t != null).Distinct().ToList();

        /// <summary>
        /// Escribe en en-US el comentario de cada segmento con título, sobrescribiendo el que tuviera.
        /// Devuelve el XML nuevo y cuántos segmentos se han comentado.
        /// </summary>
        public static (string Xml, int Comentados) Comentar(string xml, Func<string, TextosComentario> textos)
        {
            var documento = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            int comentados = 0;
            foreach (var segmento in Segmentos(documento))
            {
                string titulo = TituloSinPaso(Texto(segmento, "Title"));
                if (titulo == null)
                    continue;
                PonerTexto(documento, segmento, "Comment", textos(titulo).Comentario());
                comentados++;
            }
            var declaracion = documento.Declaration != null ? documento.Declaration + "\n" : "";
            return (declaracion + documento.Root.ToString(SaveOptions.DisableFormatting), comentados);
        }

        private static IEnumerable<XElement> Segmentos(XDocument documento) =>
            documento.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit");

        private static XElement Multilingue(XElement segmento, string composicion) =>
            segmento.Elements().Where(e => e.Name.LocalName == "ObjectList").Elements()
                .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string)e.Attribute("CompositionName") == composicion);

        private static XElement Elemento(XElement multilingue) =>
            multilingue?.Descendants().FirstOrDefault(e => e.Name.LocalName == "MultilingualTextItem"
                && e.Descendants().Any(c => c.Name.LocalName == "Culture" && c.Value == Cultura));

        private static string Texto(XElement segmento, string composicion) =>
            Elemento(Multilingue(segmento, composicion))?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value;

        // Pone el texto en el elemento en-US; si el segmento no trae comentario o le falta el idioma, se crea con IDs nuevos.
        private static void PonerTexto(XDocument documento, XElement segmento, string composicion, string texto)
        {
            var multilingue = Multilingue(segmento, composicion);
            if (multilingue == null)
            {
                var lista = segmento.Elements().FirstOrDefault(e => e.Name.LocalName == "ObjectList");
                if (lista == null)
                {
                    lista = new XElement("ObjectList");
                    segmento.Add(lista);
                }
                multilingue = new XElement("MultilingualText", new XAttribute("ID", NuevoId(documento)),
                    new XAttribute("CompositionName", composicion), new XElement("ObjectList"));
                lista.AddFirst(multilingue);
            }

            var elemento = Elemento(multilingue);
            if (elemento == null)
            {
                elemento = new XElement("MultilingualTextItem", new XAttribute("ID", NuevoId(documento)), new XAttribute("CompositionName", "Items"),
                    new XElement("AttributeList", new XElement("Culture", Cultura), new XElement("Text")));
                multilingue.Elements().First(e => e.Name.LocalName == "ObjectList").Add(elemento);
            }

            var nodoTexto = elemento.Descendants().First(e => e.Name.LocalName == "Text");
            nodoTexto.RemoveNodes();
            nodoTexto.Value = texto;
        }

        // Los ID de los objetos del XML de Openness son hexadecimales y únicos en el documento.
        private static string NuevoId(XDocument documento)
        {
            int maximo = documento.Descendants().Select(e => (string)e.Attribute("ID"))
                .Select(id => int.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int n) ? n : -1)
                .DefaultIfEmpty(-1).Max();
            return (maximo + 1).ToString("X");
        }
    }
}
