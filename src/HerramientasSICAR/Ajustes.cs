using System;
using System.Collections.Generic;
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

        /// <summary>Idioma en que están escritos los títulos de los segmentos, en el Comentador (0 castellano, 1 inglés, 2 alemán).</summary>
        public int IdiomaTitulosComentador { get; set; }

        /// <summary>Proyecto del Generador HMI que se abre al arrancar.</summary>
        public string UltimoProyectoHmi { get; set; }

        /// <summary>Carpeta donde se generaron por última vez el FC y las listas de textos.</summary>
        public string UltimaCarpetaHmiGenerada { get; set; }

        /// <summary>Por ruta de proyecto, los PLC y HMI que se eligieron leer al conectar la última vez.</summary>
        public Dictionary<string, List<string>> DispositivosLeidos { get; set; } = new Dictionary<string, List<string>>();

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
