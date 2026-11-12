using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HerramientasSICAR.Tests
{
    /// <summary>Todo texto de la interfaz (L.T en el código, {l:T} en las vistas) tiene su traducción al inglés.</summary>
    public class TextsTests
    {
        private static readonly Regex InCode = new Regex(@"L\.T\(\s*""((?:[^""\\]|\\.)*)""");
        private static readonly Regex InViews = new Regex(@"\{l:T '([^']*)'\}");

        [Fact]
        public void Texts_AllSources_HaveEnglishTranslation()
        {
            string root = RepoRoot();
            var texts = new List<string>();
            foreach (var file in SourceFiles(root, "*.cs").Where(a => Path.GetFileName(a) != "Texts.en.cs"))
                texts.AddRange(InCode.Matches(File.ReadAllText(file)).Cast<Match>().Select(m => Regex.Unescape(m.Groups[1].Value)));
            foreach (var file in SourceFiles(root, "*.axaml"))
                texts.AddRange(InViews.Matches(File.ReadAllText(file)).Cast<Match>().Select(m => m.Groups[1].Value));

            Assert.NotEmpty(texts);
            var untranslated = texts.Distinct().Where(t => !L.EnglishTexts.ContainsKey(t)).ToList();
            Assert.True(untranslated.Count == 0, "Sin traducción en Texts.en.cs:\n" + string.Join("\n", untranslated));
        }

        [Fact]
        public void EnglishTexts_Translations_KeepPlaceholders()
        {
            var placeholders = new Regex(@"\{\d+\}");
            foreach (var pair in L.EnglishTexts)
            {
                var original = placeholders.Matches(pair.Key).Cast<Match>().Select(m => m.Value).OrderBy(h => h);
                var translated = placeholders.Matches(pair.Value).Cast<Match>().Select(m => m.Value).OrderBy(h => h);
                Assert.True(original.SequenceEqual(translated), "Huecos distintos en: " + pair.Key);
            }
        }

        [Fact]
        public void T_EnglishAppName_ReturnsSicarTools()
        {
            var before = L.Language;
            try
            {
                L.Language = L.English;
                Assert.Equal("SicarTools", L.T("Herramientas SICAR"));
            }
            finally
            {
                L.Language = before;
            }
        }

        // Fuentes de la app, sin las carpetas de compilación.
        private static IEnumerable<string> SourceFiles(string root, string pattern) =>
            Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
                .Where(a => !a.Split(Path.DirectorySeparatorChar).Any(c => c == "bin" || c == "obj"));

        // Carpeta src/HerramientasSICAR, buscando la raíz del repo (la del .sln) hacia arriba desde los tests.
        private static string RepoRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "HerramientasSICAR.sln")))
                folder = folder.Parent;
            if (folder == null)
                throw new InvalidOperationException("No se encuentra HerramientasSICAR.sln");
            return Path.Combine(folder.FullName, "src", "HerramientasSICAR");
        }
    }
}
