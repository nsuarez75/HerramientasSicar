using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HerramientasSICAR.Logic
{
    /// <summary>
    /// Idioma de un título. El orden es el de la lista de la app y lo que se guarda en los ajustes: los nuevos van al final.
    /// </summary>
    public enum TitleLanguage { Castellano, English, German, Polish, Turkish, French }

    /// <summary>Las líneas del comentario de un segmento, una por idioma.</summary>
    public sealed class CommentTexts
    {
        private readonly IReadOnlyDictionary<TitleLanguage, string> _texts;

        public CommentTexts(IReadOnlyDictionary<TitleLanguage, string> texts)
        {
            _texts = texts;
        }

        public CommentTexts(string english, string german, string spanish)
            : this(new Dictionary<TitleLanguage, string>
            {
                [TitleLanguage.English] = english,
                [TitleLanguage.German] = german,
                [TitleLanguage.Castellano] = spanish,
            })
        {
        }

        /// <summary>Comentario del segmento, en el formato de SICAR: una línea por idioma, en el orden de SICAR.</summary>
        public string CommentText() =>
            string.Join("\n", SequenceComments.CommentOrder.Where(_texts.ContainsKey)
                .Select(l => "Title_" + SequenceComments.Tag(l) + " " + _texts[l]));
    }

    /// <summary>
    /// Comentador: reglas para comentar los segmentos de los FB de secuencia a partir de su título, sobre el XML
    /// exportado con Openness. No depende de TIA Portal.
    /// </summary>
    public static class SequenceComments
    {
        /// <summary>Carpeta de TIA donde están los FB de secuencia y de mensajes.</summary>
        public const string SequencesFolder = "10_Sequence & Messageblocks";

        /// <summary>Idioma del proyecto: de él se lee el título y en él se escribe el comentario.</summary>
        public const string Culture = "en-US";

        private static readonly Regex StepNetwork = new Regex(@"\bS\d{3}\b");
        private static readonly Regex Om = new Regex(@"^OM[1-8]$");

        /// <summary>Orden de las líneas en el comentario.</summary>
        public static readonly IReadOnlyList<TitleLanguage> CommentOrder = new[]
        {
            TitleLanguage.English, TitleLanguage.German, TitleLanguage.Castellano,
            TitleLanguage.Polish, TitleLanguage.Turkish, TitleLanguage.French,
        };

        /// <summary>Las tres líneas que lleva siempre el comentario; las demás, solo si se traduce.</summary>
        private static readonly TitleLanguage[] BaseLanguages = { TitleLanguage.English, TitleLanguage.German, TitleLanguage.Castellano };

        /// <summary>Código del traductor para cada idioma de los títulos.</summary>
        public static string Code(TitleLanguage language)
        {
            switch (language)
            {
                case TitleLanguage.English: return "en";
                case TitleLanguage.German: return "de";
                case TitleLanguage.Polish: return "pl";
                case TitleLanguage.Turkish: return "tr";
                case TitleLanguage.French: return "fr";
                default: return "es";
            }
        }

        /// <summary>Lo que va tras «Title_» en la línea de cada idioma.</summary>
        public static string Tag(TitleLanguage language)
        {
            switch (language)
            {
                case TitleLanguage.English: return "english";
                case TitleLanguage.German: return "deutsch";
                case TitleLanguage.Polish: return "polski";
                case TitleLanguage.Turkish: return "tuerk";
                case TitleLanguage.French: return "francais";
                default: return "espanol";
            }
        }

        /// <summary>
        /// Idiomas que lleva el comentario: traduciendo, todos; sin traducir, los tres de siempre más el del título (con el
        /// mismo texto en todos).
        /// </summary>
        public static IReadOnlyList<TitleLanguage> Languages(TitleLanguage source, bool translate) =>
            CommentOrder.Where(l => translate || BaseLanguages.Contains(l) || l == source).ToList();

        /// <summary>Un bloque entra en la lista del Comentador si su nombre acaba en _FB.</summary>
        public static bool IsFb(string name) => name.EndsWith("_FB", StringComparison.Ordinal);

        /// <summary>
        /// Si sale marcado al listar: un FB de secuencia de una estación (bajo una carpeta OM1 a OM8), que no sea de
        /// Summary, Messages ni de la carpeta 50_Safety. Es el filtro que usaba el Comentador con el Excel.
        /// </summary>
        /// <param name="grupos">Carpetas desde la de secuencias (sin incluirla) hasta la del bloque.</param>
        public static bool CheckedByDefault(IReadOnlyList<string> groups, string name) =>
            IsFb(name)
            && groups.Any(g => Om.IsMatch(g))
            && !groups.Any(g => g.Contains("50_Safety"))
            && !name.Contains("Summary")
            && !name.Contains("Messages");

        // Separadores de segmentos («==== SEQUENCE ====», «------»): no se comentan.
        private static readonly Regex Separator = new Regex(@"^\s*[=\-*#]{3,}");

        /// <summary>Título sin el número de paso (S###), o null si es un separador o no queda ningún texto que comentar.</summary>
        public static string TitleWithoutStep(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || Separator.IsMatch(title))
                return null;
            string clean = Regex.Replace(StepNetwork.Replace(title, ""), @"\s+", " ").Trim();
            return clean.Any(char.IsLetter) ? clean : null;
        }

        /// <summary>Los nombres de equipo (con «++») no se traducen: el traductor les cambia espacios y letras.</summary>
        public static bool IsEquipmentName(string text) => text != null && text.Contains("++");

        /// <summary>Títulos distintos (ya sin S###) de los segmentos que se van a comentar.</summary>
        public static IReadOnlyList<string> Titles(string xml) =>
            Networks(XDocument.Parse(xml)).Select(s => TitleWithoutStep(Text(s, "Title")))
                .Where(t => t != null).Distinct().ToList();

        /// <summary>
        /// Escribe en en-US el comentario de cada segmento con título, sobrescribiendo el que tuviera.
        /// Devuelve el XML nuevo y cuántos segmentos se han comentado.
        /// </summary>
        public static (string Xml, int Commented) Comment(string xml, Func<string, CommentTexts> texts)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            int commented = 0;
            foreach (var network in Networks(document))
            {
                string title = TitleWithoutStep(Text(network, "Title"));
                if (title == null)
                    continue;
                SetText(document, network, "Comment", texts(title).CommentText());
                commented++;
            }
            var declaration = document.Declaration != null ? document.Declaration + "\n" : "";
            return (declaration + document.Root.ToString(SaveOptions.DisableFormatting), commented);
        }

        private static IEnumerable<XElement> Networks(XDocument document) =>
            document.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit");

        private static XElement Multilingual(XElement network, string composition) =>
            network.Elements().Where(e => e.Name.LocalName == "ObjectList").Elements()
                .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string)e.Attribute("CompositionName") == composition);

        private static XElement Item(XElement multilingual) =>
            multilingual?.Descendants().FirstOrDefault(e => e.Name.LocalName == "MultilingualTextItem"
                && e.Descendants().Any(c => c.Name.LocalName == "Culture" && c.Value == Culture));

        private static string Text(XElement network, string composition) =>
            Item(Multilingual(network, composition))?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value;

        // Pone el texto en el elemento en-US; si el segmento no trae comentario o le falta el idioma, se crea con IDs nuevos.
        private static void SetText(XDocument document, XElement network, string composition, string text)
        {
            var multilingual = Multilingual(network, composition);
            if (multilingual == null)
            {
                var list = network.Elements().FirstOrDefault(e => e.Name.LocalName == "ObjectList");
                if (list == null)
                {
                    list = new XElement("ObjectList");
                    network.Add(list);
                }
                multilingual = new XElement("MultilingualText", new XAttribute("ID", NewId(document)),
                    new XAttribute("CompositionName", composition), new XElement("ObjectList"));
                list.AddFirst(multilingual);
            }

            var item = Item(multilingual);
            if (item == null)
            {
                item = new XElement("MultilingualTextItem", new XAttribute("ID", NewId(document)), new XAttribute("CompositionName", "Items"),
                    new XElement("AttributeList", new XElement("Culture", Culture), new XElement("Text")));
                multilingual.Elements().First(e => e.Name.LocalName == "ObjectList").Add(item);
            }

            var textNode = item.Descendants().First(e => e.Name.LocalName == "Text");
            textNode.RemoveNodes();
            textNode.Value = text;
        }

        // Los ID de los objetos del XML de Openness son hexadecimales y únicos en el documento.
        private static string NewId(XDocument document)
        {
            int maximum = document.Descendants().Select(e => (string)e.Attribute("ID"))
                .Select(id => int.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int n) ? n : -1)
                .DefaultIfEmpty(-1).Max();
            return (maximum + 1).ToString("X");
        }
    }
}
