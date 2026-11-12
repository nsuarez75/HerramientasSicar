using GTranslate.Translators;
using HerramientasSICAR.Logica;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Helpers
{
    /// <summary>Traductor en línea (servicios de terceros, como Google Translate) que usan el Comentador y el Generador HMI.</summary>
    public static class Traductor
    {
        /// <summary>Traducciones en paralelo como máximo.</summary>
        public const int MaxConcurrentes = 20;

        private static readonly ITranslator Servicio = new AggregateTranslator();

        /// <summary>
        /// Traduce muchos textos en paralelo (como mucho <see cref="MaxConcurrentes"/> a la vez). Devuelve, por cada par
        /// (texto, destino), su traducción; <paramref name="progreso"/> recibe (hechas, total).
        /// </summary>
        public static async Task<Dictionary<(string Texto, string Destino), string>> TraducirVariosAsync(
            IEnumerable<(string Texto, string Destino)> pares, string origen = null, IProgress<(int Hechas, int Total)> progreso = null)
        {
            var lista = pares.Distinct().ToList();
            var traducciones = new ConcurrentDictionary<(string, string), string>();
            int hechas = 0;
            progreso?.Report((0, lista.Count));
            using (var semaforo = new SemaphoreSlim(MaxConcurrentes))
            {
                await Task.WhenAll(lista.Select(async par =>
                {
                    await semaforo.WaitAsync();
                    try
                    {
                        traducciones[par] = await TraducirAsync(par.Texto, par.Destino, origen);
                    }
                    finally
                    {
                        semaforo.Release();
                        progreso?.Report((Interlocked.Increment(ref hechas), lista.Count));
                    }
                }));
            }
            return new Dictionary<(string, string), string>(traducciones);
        }

        /// <summary>
        /// Traduce el texto a <paramref name="destino"/> («en», «de», «es»). Sin <paramref name="origen"/>, el idioma se
        /// adivina. Los nombres de equipo (con «++») no se traducen, y si el traductor falla (sin conexión, límite de
        /// peticiones…) se devuelve el texto tal cual.
        /// </summary>
        public static async Task<string> TraducirAsync(string texto, string destino, string origen = null)
        {
            if (string.IsNullOrWhiteSpace(texto) || ComentariosSecuencia.EsNombreDeEquipo(texto) || destino == origen)
                return texto;
            try
            {
                var resultado = await Servicio.TranslateAsync(texto, destino, origen);
                return resultado.Translation;
            }
            catch
            {
                return texto;
            }
        }
    }
}
