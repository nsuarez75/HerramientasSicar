using System;
using System.Collections.Generic;
using System.Globalization;

namespace HerramientasSICAR
{
    /// <summary>
    /// Idioma de la interfaz. Los textos se escriben en castellano en el código y L.T los traduce:
    /// el texto en castellano es la clave, así que un texto sin traducción se muestra tal cual.
    /// </summary>
    public static partial class L
    {
        public const string Castellano = "es";
        public const string English = "en";

        public static string Language { get; set; } = Castellano;

        public static string Normalize(string language) => language == English ? English : Castellano;

        /// <summary>Idioma de Windows: inglés salvo que sea castellano.</summary>
        public static string SystemLanguage() =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Castellano ? Castellano : English;

        public static IReadOnlyDictionary<string, string> EnglishTexts => En;

        public static string T(string text) => Language == English && En.TryGetValue(text, out var translated) ? translated : text;

        /// <summary>Textos con huecos {0}, {1}...: se traduce el patrón y después se rellenan.</summary>
        public static string T(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(format), args);
    }
}
