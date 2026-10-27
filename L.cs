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
        public const string Ingles = "en";

        public static string Idioma { get; set; } = Castellano;

        public static string Normalizar(string idioma) => idioma == Ingles ? Ingles : Castellano;

        /// <summary>Idioma de Windows: inglés salvo que sea castellano.</summary>
        public static string IdiomaDelSistema() =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Castellano ? Castellano : Ingles;

        public static IReadOnlyDictionary<string, string> TextosEnIngles => En;

        public static string T(string texto) => Idioma == Ingles && En.TryGetValue(texto, out var traducido) ? traducido : texto;

        /// <summary>Textos con huecos {0}, {1}...: se traduce el patrón y después se rellenan.</summary>
        public static string T(string formato, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(formato), args);
    }
}
