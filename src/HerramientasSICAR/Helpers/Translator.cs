using GTranslate.Translators;
using HerramientasSICAR.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Helpers
{
    /// <summary>Traductor en línea (servicios de terceros, como Google Translate) que usan el Comentador y el Generador HMI.</summary>
    public static class Translator
    {
        /// <summary>Traducciones en paralelo como máximo.</summary>
        public const int MaxConcurrent = 20;

        private static readonly ITranslator Service = new AggregateTranslator();

        /// <summary>
        /// Traduce muchos textos en paralelo (como mucho <see cref="MaxConcurrentes"/> a la vez). Devuelve, por cada par
        /// (texto, destino), su traducción; <paramref name="progreso"/> recibe (hechas, total).
        /// </summary>
        public static async Task<Dictionary<(string Text, string Target), string>> TranslateManyAsync(
            IEnumerable<(string Text, string Target)> pairList, string source = null, IProgress<(int Done, int Total)> progress = null)
        {
            var list = pairList.Distinct().ToList();
            var translations = new ConcurrentDictionary<(string, string), string>();
            int done = 0;
            progress?.Report((0, list.Count));
            using (var semaphore = new SemaphoreSlim(MaxConcurrent))
            {
                await Task.WhenAll(list.Select(async pair =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        translations[pair] = await TranslateTextAsync(pair.Text, pair.Target, source);
                    }
                    finally
                    {
                        semaphore.Release();
                        progress?.Report((Interlocked.Increment(ref done), list.Count));
                    }
                }));
            }
            return new Dictionary<(string, string), string>(translations);
        }

        /// <summary>
        /// Traduce el texto a <paramref name="destino"/> («en», «de», «es»). Sin <paramref name="origen"/>, el idioma se
        /// adivina. Los nombres de equipo (con «++») no se traducen, y si el traductor falla (sin conexión, límite de
        /// peticiones…) se devuelve el texto tal cual.
        /// </summary>
        public static async Task<string> TranslateTextAsync(string text, string target, string source = null)
        {
            if (string.IsNullOrWhiteSpace(text) || SequenceComments.IsEquipmentName(text) || target == source)
                return text;
            try
            {
                var result = await Service.TranslateAsync(text, target, source);
                return result.Translation;
            }
            catch
            {
                return text;
            }
        }
    }
}
