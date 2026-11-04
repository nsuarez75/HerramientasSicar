#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Nombres de paso y de mensaje en varios idiomas. SICAR los escribe en el comentario de la red, una línea
// por idioma: «Title_espanol Bajar ventana», «Title_english Lower window»…
using System.Text.RegularExpressions;
using HerramientasSICAR.Nucleo.Lad;

namespace HerramientasSICAR.Nucleo.Sicar;

public static partial class StepTitles
{
    // Idioma (lo que va tras «Title_», en minúsculas) → texto. Si no hay líneas Title_, queda vacío.
    public static IReadOnlyDictionary<string, string> Parse(string? comment)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(comment))
        {
            return titles;
        }
        foreach (Match match in TitleLine().Matches(comment))
        {
            var language = match.Groups[1].Value.ToLowerInvariant();
            var text = match.Groups[2].Value.Trim();
            if (text.Length > 0 && !titles.ContainsKey(language))
            {
                titles[language] = text;
            }
        }
        return titles;
    }

    // Títulos del comentario de la red. Solo vale el texto del idioma de edición del proyecto de TIA
    // (el que se ve en el editor): las demás culturas suelen guardar restos antiguos que TIA no muestra,
    // a veces copiados de otra secuencia. Sin idioma de edición, la cultura con más títulos rellenos.
    public static IReadOnlyDictionary<string, string> Parse(MultilingualText comment, string? editingCulture = null)
    {
        if (editingCulture != null && comment.Items.TryGetValue(editingCulture, out var text))
        {
            return Parse(text);
        }
        return comment.Items
            .Select(item => (item.Key, Titles: Parse(item.Value)))
            .OrderByDescending(c => c.Titles.Count)
            .ThenBy(c => c.Key == "en-US" ? 0 : 1)
            .Select(c => c.Titles)
            .FirstOrDefault() ?? new Dictionary<string, string>();
    }

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex TitleLine() => _TitleLine ??= new Regex(@"^\s*Title_([A-Za-z]+)[ \t]+(.*?)\s*$", RegexOptions.Multiline);

    private static Regex? _TitleLine;
}
