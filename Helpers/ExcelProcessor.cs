using ClosedXML.Excel;
using GTranslate.Translators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Helpers
{
    public static class ExcelProcessor
    {
        private const int MaxTraduccionesConcurrentes = 20;

        private static readonly ITranslator Translator = new AggregateTranslator();

        static ExcelProcessor()
        {
            // .NET Framework limita por defecto a 2 conexiones HTTP simultáneas por host,
            // lo que anula cualquier paralelismo real al traducir. Sin este ajuste, 20 tareas
            // en paralelo tardan casi lo mismo que en secuencial.
            if (ServicePointManager.DefaultConnectionLimit < MaxTraduccionesConcurrentes * 2)
                ServicePointManager.DefaultConnectionLimit = MaxTraduccionesConcurrentes * 2;
        }

        public static async Task<string> TraducirAsync(string texto, string idiomaDestino)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto;

            try
            {
                // fromLanguage = null -> auto-detección: el texto base puede no estar realmente en inglés.
                var resultado = await Translator.TranslateAsync(texto, idiomaDestino, null);
                return resultado.Translation;
            }
            catch
            {
                // Si falla la traducción (p.ej. sin conexión o rate-limit), se conserva el texto original.
                return texto;
            }
        }

        public static async Task ModificarTodosComentariosAsync(string inputPath, string outputPath, bool traducir = true, IProgress<(int completados, int total)> progress = null)
        {
            // 1. Read Data Phase
            var copyWsUser = new List<List<string>>();
            var copyWsSystem = new List<List<string>>();

            using (var workbook = new XLWorkbook(inputPath))
            {
                if (!workbook.Worksheets.TryGetWorksheet("User Texts", out var wsUser))
                    throw new Exception("La hoja 'User Texts' no existe.");

                // Read User Texts into memory
                foreach (var row in wsUser.RangeUsed().Rows())
                {
                    var rowData = new List<string>();
                    int lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

                    for(int c = 1; c <= lastCol; c++)
                    {
                        rowData.Add(row.Cell(c).Value.ToString());
                    }
                    copyWsUser.Add(rowData);
                }

                // Read System Texts if exists
                if (workbook.Worksheets.TryGetWorksheet("System Texts", out var wsSystem))
                {
                    foreach (var row in wsSystem.RangeUsed().Rows())
                    {
                        var rowData = new List<string>();
                        int lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

                        for (int c = 1; c <= lastCol; c++)
                        {
                            rowData.Add(row.Cell(c).Value.ToString());
                        }
                        copyWsSystem.Add(rowData);
                    }
                }
            }

            // 2. Processing Phase (On Memory Data)
            // Headers are row 0
            if (copyWsUser.Count > 0)
            {
                var headers = copyWsUser[0];
                int viewPathIdx = headers.FindIndex(h => h.Equals("ViewPath", StringComparison.OrdinalIgnoreCase));
                int enUsStarIdx = headers.FindIndex(h => h.Equals("en-US*", StringComparison.OrdinalIgnoreCase));
                int enUsIdx = headers.FindIndex(h => h.Equals("en-US", StringComparison.OrdinalIgnoreCase));

                if (viewPathIdx != -1 && enUsStarIdx != -1 && enUsIdx != -1)
                {
                    var titleMap = new Dictionary<string, string>();
                    var omRegex = new Regex(@"OM[1-8]\\");
                    var stepRegex = new Regex(@"\bS\d{3}\b");

                    // First pass: Find Titles
                    for (int i = 1; i < copyWsUser.Count; i++)
                    {
                        // Safely get ViewPath
                        if (viewPathIdx >= copyWsUser[i].Count) continue;
                        string viewPath = copyWsUser[i][viewPathIdx];

                        // Filter
                         bool matchesFilter =
                            viewPath.Contains("10_Sequence & Messageblocks") &&
                            omRegex.IsMatch(viewPath) &&
                            viewPath.Contains("_FB") &&
                            !viewPath.Contains("Summary") &&
                            !viewPath.Contains("Messages") &&
                            !viewPath.Contains("50_Safety");

                        if (!matchesFilter) continue;

                        if (viewPath.EndsWith(@"\Title"))
                        {
                            string rutaBase = viewPath.Replace(@"\Title", "");
                            string titleText = (enUsStarIdx < copyWsUser[i].Count) ? copyWsUser[i][enUsStarIdx] : "";
                            titleMap[rutaBase] = titleText;
                        }
                    }

                    // Second pass: Collect which rows need translation and the distinct titles involved
                    var filasATraducir = new List<int>();
                    var tituloPorFila = new Dictionary<int, string>();
                    var titulosUnicos = new HashSet<string>();

                    for (int i = 1; i < copyWsUser.Count; i++)
                    {
                        if (viewPathIdx >= copyWsUser[i].Count) continue;
                        string viewPath = copyWsUser[i][viewPathIdx];

                         bool matchesFilter =
                            viewPath.Contains("10_Sequence & Messageblocks") &&
                            omRegex.IsMatch(viewPath) &&
                            viewPath.Contains("_FB") &&
                            !viewPath.Contains("Summary") &&
                            !viewPath.Contains("Messages") &&
                            !viewPath.Contains("50_Safety");

                        if (!matchesFilter) continue;

                        if (viewPath.EndsWith(@"\Comment"))
                        {
                            string rutaBase = viewPath.Replace(@"\Comment", "");
                            if (titleMap.TryGetValue(rutaBase, out string titulo))
                            {
                                string tituloSinStep = stepRegex.Replace(titulo, "").Trim();
                                if (!string.IsNullOrEmpty(tituloSinStep))
                                {
                                    filasATraducir.Add(i);
                                    tituloPorFila[i] = tituloSinStep;
                                    titulosUnicos.Add(tituloSinStep);
                                }
                            }
                        }
                    }

                    // Third pass: resolve every distinct título into its en/de/es text.
                    // If "traducir" is off, keep the legacy behavior: copy the same text into all three languages.
                    string[] idiomas = { "en", "de", "es" };
                    var pares = titulosUnicos
                        .SelectMany(titulo => idiomas.Select(idioma => (titulo, idioma)))
                        .ToList();

                    var traducciones = new ConcurrentDictionary<(string titulo, string idioma), string>();
                    int total = pares.Count;
                    int completados = 0;
                    progress?.Report((0, total));

                    if (traducir)
                    {
                        using (var semaphore = new SemaphoreSlim(MaxTraduccionesConcurrentes))
                        {
                            var tareas = pares.Select(async par =>
                            {
                                await semaphore.WaitAsync();
                                try
                                {
                                    traducciones[par] = await TraducirAsync(par.titulo, par.idioma);
                                }
                                finally
                                {
                                    semaphore.Release();
                                    int hechos = Interlocked.Increment(ref completados);
                                    progress?.Report((hechos, total));
                                }
                            });

                            await Task.WhenAll(tareas);
                        }
                    }
                    else
                    {
                        foreach (var par in pares)
                            traducciones[par] = par.titulo;

                        progress?.Report((total, total));
                    }

                    // Fourth pass: apply the translated texts to each row
                    foreach (int i in filasATraducir)
                    {
                        string tituloSinStep = tituloPorFila[i];
                        string tituloIngles = traducciones[(tituloSinStep, "en")];
                        string tituloAleman = traducciones[(tituloSinStep, "de")];
                        string tituloEspanol = traducciones[(tituloSinStep, "es")];

                        string nuevoTexto = $"Title_english {tituloIngles}\n" +
                                          $"Title_deutsch {tituloAleman}\n" +
                                          $"Title_espanol {tituloEspanol}";

                        // Ensure list is big enough (it should be)
                        while (copyWsUser[i].Count <= Math.Max(enUsStarIdx, enUsIdx))
                            copyWsUser[i].Add("");

                        copyWsUser[i][enUsStarIdx] = nuevoTexto;
                        copyWsUser[i][enUsIdx] = nuevoTexto;
                    }
                }
            }

            // 3. Write Output Phase (New Workbook)
            using (var newWorkbook = new XLWorkbook())
            {
                // User Texts
                var wsUser = newWorkbook.AddWorksheet("User Texts");
                for (int r = 0; r < copyWsUser.Count; r++)
                {
                    for (int c = 0; c < copyWsUser[r].Count; c++)
                    {
                        wsUser.Cell(r + 1, c + 1).Value = copyWsUser[r][c];
                    }
                }

                // System Texts
                if (copyWsSystem.Count > 0)
                {
                    var wsSystem = newWorkbook.AddWorksheet("System Texts");
                    for (int r = 0; r < copyWsSystem.Count; r++)
                    {
                        for (int c = 0; c < copyWsSystem[r].Count; c++)
                        {
                            wsSystem.Cell(r + 1, c + 1).Value = copyWsSystem[r][c];
                        }
                    }
                }

                newWorkbook.SaveAs(outputPath);
            }
        }
    }
}
