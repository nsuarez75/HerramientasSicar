using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.Logic
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
    public static class VersionCheck
    {
        public const string Repository = "nsuarez75/HerramientasSicar";
        public const string RepositoryUrl = "https://github.com/" + Repository;

        public static async Task<Release> LatestRelease(HttpClient http, CancellationToken cancel = default)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest"))
                {
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("HerramientasSICAR", "1"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    using (var response = await http.SendAsync(request, cancel))
                    {
                        if (!response.IsSuccessStatusCode)
                            return null;
                        return Read(await response.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>Respuesta de /releases/latest: tag_name (v1.2.0) y html_url.</summary>
        public static Release Read(string json)
        {
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                    string url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
                    var version = tag == null ? null : ParseVersion(tag);
                    return version == null ? null : new Release(tag, version, url ?? RepositoryUrl + "/releases");
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>v1.2 o 1.2.3 -> versión con tres números.</summary>
        public static Version ParseVersion(string text)
        {
            string clean = text.Trim().TrimStart('v', 'V');
            int dash = clean.IndexOfAny(new[] { '-', '+' });
            if (dash >= 0)
                clean = clean.Substring(0, dash);

            var parts = clean.Split('.');
            if (parts.Length < 1 || parts.Length > 4 ||
                !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                return null;

            var numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).Concat(new[] { 0, 0 }).Take(3).ToArray();
            return new Version(numbers[0], numbers[1], numbers[2]);
        }

        public static bool IsNewer(Release release, Version current) =>
            release.Version > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
    }
}
