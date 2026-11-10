using GTranslate.Translators;
using HerramientasSICAR.Logica;
using System.Net;
using System.Threading.Tasks;

namespace HerramientasSICAR.Helpers
{
    /// <summary>Traductor en línea (servicios de terceros, como Google Translate) que usan el Comentador y el Generador HMI.</summary>
    public static class Traductor
    {
        /// <summary>Traducciones en paralelo como máximo.</summary>
        public const int MaxConcurrentes = 20;

        private static readonly ITranslator Servicio = new AggregateTranslator();

        static Traductor()
        {
            // .NET Framework limita por defecto a 2 conexiones HTTP simultáneas por host, lo que anula el paralelismo.
            if (ServicePointManager.DefaultConnectionLimit < MaxConcurrentes * 2)
                ServicePointManager.DefaultConnectionLimit = MaxConcurrentes * 2;
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
