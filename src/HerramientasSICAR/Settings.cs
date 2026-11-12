using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HerramientasSICAR
{
    /// <summary>Preferencias de la interfaz, guardadas en %AppData%\HerramientasSICAR\ajustes.json.</summary>
    public sealed class Settings
    {
        public const string SystemTheme = "system";
        public const string LightTheme = "light";
        public const string DarkTheme = "dark";

        [JsonPropertyName("Tema")]
        public string Theme { get; set; } = SystemTheme;

        /// <summary>Vacío: el idioma de Windows.</summary>
        [JsonPropertyName("Idioma")]
        public string Language { get; set; }

        [JsonPropertyName("UltimaCarpeta")]
        public string LastFolder { get; set; }

        /// <summary>Idioma en que están escritos los títulos de los segmentos, en el Comentador (0 castellano, 1 inglés, 2 alemán).</summary>
        [JsonPropertyName("IdiomaTitulosComentador")]
        public int CommentatorTitleLanguage { get; set; }

        /// <summary>Proyecto del Generador HMI que se abre al arrancar.</summary>
        [JsonPropertyName("UltimoProyectoHmi")]
        public string LastHmiProject { get; set; }

        /// <summary>Carpeta donde se generaron por última vez el FC y las listas de textos.</summary>
        [JsonPropertyName("UltimaCarpetaHmiGenerada")]
        public string LastHmiOutputFolder { get; set; }

        /// <summary>Carpeta donde se guardó por última vez un Excel de DiagExpected.</summary>
        [JsonPropertyName("UltimaCarpetaDiagExpected")]
        public string LastDiagExpectedFolder { get; set; }

        /// <summary>Por ruta de proyecto, los PLC y HMI que se eligieron leer al conectar la última vez.</summary>
        [JsonPropertyName("DispositivosLeidos")]
        public Dictionary<string, List<string>> ReadDevices { get; set; } = new Dictionary<string, List<string>>();

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "ajustes.json");

        public static Settings Load()
        {
            try
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return new Settings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Los ajustes son una comodidad: si no se pueden guardar, la app sigue igual.
            }
        }
    }
}
