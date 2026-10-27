using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HerramientasSICAR.Tests
{
    /// <summary>Todo texto de la interfaz (L.T en el código, {l:T} en las vistas) tiene su traducción al inglés.</summary>
    public class TextosTests
    {
        private static readonly Regex EnCodigo = new Regex(@"L\.T\(\s*""((?:[^""\\]|\\.)*)""");
        private static readonly Regex EnVistas = new Regex(@"\{l:T '([^']*)'\}");

        [Fact]
        public void Todos_los_textos_tienen_traduccion_al_ingles()
        {
            string raiz = RaizDelProyecto();
            var textos = new List<string>();
            foreach (var archivo in Fuentes(raiz, "*.cs").Where(a => Path.GetFileName(a) != "Textos.en.cs"))
                textos.AddRange(EnCodigo.Matches(File.ReadAllText(archivo)).Cast<Match>().Select(m => Regex.Unescape(m.Groups[1].Value)));
            foreach (var archivo in Fuentes(raiz, "*.axaml"))
                textos.AddRange(EnVistas.Matches(File.ReadAllText(archivo)).Cast<Match>().Select(m => m.Groups[1].Value));

            Assert.NotEmpty(textos);
            var sinTraducir = textos.Distinct().Where(t => !L.TextosEnIngles.ContainsKey(t)).ToList();
            Assert.True(sinTraducir.Count == 0, "Sin traducción en Textos.en.cs:\n" + string.Join("\n", sinTraducir));
        }

        [Fact]
        public void Las_traducciones_tienen_los_mismos_huecos()
        {
            var huecos = new Regex(@"\{\d+\}");
            foreach (var par in L.TextosEnIngles)
            {
                var original = huecos.Matches(par.Key).Cast<Match>().Select(m => m.Value).OrderBy(h => h);
                var traducido = huecos.Matches(par.Value).Cast<Match>().Select(m => m.Value).OrderBy(h => h);
                Assert.True(original.SequenceEqual(traducido), "Huecos distintos en: " + par.Key);
            }
        }

        [Fact]
        public void En_ingles_la_app_se_llama_SicarTools()
        {
            var antes = L.Idioma;
            try
            {
                L.Idioma = L.Ingles;
                Assert.Equal("SicarTools", L.T("Herramientas SICAR"));
            }
            finally
            {
                L.Idioma = antes;
            }
        }

        // Fuentes de la app, sin las carpetas de compilación ni las de los tests.
        private static IEnumerable<string> Fuentes(string raiz, string patron) =>
            Directory.GetFiles(raiz, patron, SearchOption.AllDirectories)
                .Where(a => !a.Split(Path.DirectorySeparatorChar).Any(c => c == "bin" || c == "obj" || c == "HerramientasSICAR.Tests"));

        private static string RaizDelProyecto()
        {
            var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
            while (carpeta != null && !File.Exists(Path.Combine(carpeta.FullName, "HerramientasSICAR.csproj")))
                carpeta = carpeta.Parent;
            return carpeta?.FullName ?? throw new InvalidOperationException("No se encuentra HerramientasSICAR.csproj");
        }
    }
}
