using System;
using System.IO;
using System.Text.Json;

namespace HerramientasSICAR
{
    /// <summary>Preferencias de la interfaz, guardadas en %AppData%\HerramientasSICAR\ajustes.json.</summary>
    public sealed class Ajustes
    {
        public const string TemaSistema = "system";
        public const string TemaClaro = "light";
        public const string TemaOscuro = "dark";

        public string Tema { get; set; } = TemaSistema;

        /// <summary>Vacío: el idioma de Windows.</summary>
        public string Idioma { get; set; }

        public string UltimaCarpeta { get; set; }

        private static string Ruta => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "ajustes.json");

        public static Ajustes Cargar()
        {
            try
            {
                return JsonSerializer.Deserialize<Ajustes>(File.ReadAllText(Ruta)) ?? new Ajustes();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return new Ajustes();
            }
        }

        public void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
                File.WriteAllText(Ruta, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Los ajustes son una comodidad: si no se pueden guardar, la app sigue igual.
            }
        }
    }
}
