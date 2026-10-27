using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Logica
{
    public sealed class Release
    {
        public Release(string tag, Version version, string url)
        {
            Tag = tag;
            Version = version;
            Url = url;
        }

        public string Tag { get; }
        public Version Version { get; }
        public string Url { get; }
    }

    /// <summary>
    /// Comprobación de versión nueva: pregunta a GitHub por la última release del repo y la compara con la de la app.
    /// Si no hay red, el repo no es accesible o la respuesta no se entiende, no dice nada.
    /// </summary>
    public static class ComprobarVersion
    {
        public const string Repositorio = "nsuarez75/HerramientasSicar";
        public const string UrlRepositorio = "https://github.com/" + Repositorio;

        public static async Task<Release> UltimaRelease(HttpClient http, CancellationToken cancelar = default)
        {
            try
            {
                using (var peticion = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repositorio}/releases/latest"))
                {
                    peticion.Headers.UserAgent.Add(new ProductInfoHeaderValue("HerramientasSICAR", "1"));
                    peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    using (var respuesta = await http.SendAsync(peticion, cancelar))
                    {
                        if (!respuesta.IsSuccessStatusCode)
                            return null;
                        return Leer(await respuesta.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>Respuesta de /releases/latest: tag_name (v1.2.0) y html_url.</summary>
        public static Release Leer(string json)
        {
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var raiz = doc.RootElement;
                    string tag = raiz.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                    string url = raiz.TryGetProperty("html_url", out var u) ? u.GetString() : null;
                    var version = tag == null ? null : LeerVersion(tag);
                    return version == null ? null : new Release(tag, version, url ?? UrlRepositorio + "/releases");
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>v1.2 o 1.2.3 -> versión con tres números.</summary>
        public static Version LeerVersion(string texto)
        {
            string limpio = texto.Trim().TrimStart('v', 'V');
            int guion = limpio.IndexOfAny(new[] { '-', '+' });
            if (guion >= 0)
                limpio = limpio.Substring(0, guion);

            var partes = limpio.Split('.');
            if (partes.Length < 1 || partes.Length > 4 ||
                !partes.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                return null;

            var numeros = partes.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).Concat(new[] { 0, 0 }).Take(3).ToArray();
            return new Version(numeros[0], numeros[1], numeros[2]);
        }

        public static bool EsMasNueva(Release release, Version actual) =>
            release.Version > new Version(actual.Major, actual.Minor, Math.Max(actual.Build, 0));
    }
}
