using HerramientasSICAR.Nucleo.Sicar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logica
{
    /// <summary>
    /// sincro.json, junto al programa exportado de un PLC: el idioma de edición con el que se leen los nombres de los
    /// pasos y las señales que el usuario ha marcado como excluyentes (las decisiones de Sincro Checker).
    /// </summary>
    public sealed class ArchivoSincro
    {
        public const string Nombre = "sincro.json";

        /// <summary>Idioma de edición de GST-Diag cuando no se sabe (carpeta exportada con otra herramienta).</summary>
        public const string IdiomaPorDefecto = "en-US";

        public string IdiomaEdicion { get; set; } = IdiomaPorDefecto;

        public List<GrupoGuardado> Excluyentes { get; set; } = new List<GrupoGuardado>();

        public sealed class GrupoGuardado
        {
            public List<string> Senales { get; set; } = new List<string>();
            public string Nota { get; set; }
        }

        public List<ExclusiveGroup> Grupos() =>
            Excluyentes.Select(g => new ExclusiveGroup(g.Senales) { Note = g.Nota }).ToList();

        public void PonerGrupos(IEnumerable<ExclusiveGroup> grupos) =>
            Excluyentes = grupos.Select(g => new GrupoGuardado { Senales = g.Signals.ToList(), Nota = g.Note }).ToList();

        /// <summary>sincro.json dentro de una carpeta exportada a mano (la que se abre con «Abrir carpeta»).</summary>
        public static string EnCarpeta(string carpeta) => Path.Combine(carpeta, Nombre);

        /// <summary>
        /// Archivo de un PLC exportado desde TIA: %AppData%\HerramientasSICAR\Sincro\&lt;proyecto&gt;\&lt;PLC&gt;.json, para no
        /// dejar nada en la carpeta del proyecto.
        /// </summary>
        public static string EnAppData(string proyecto, string plc) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "Sincro",
                FileRules.SafeName(proyecto), FileRules.SafeName(plc) + ".json");

        /// <summary>El de esa ruta, o uno vacío si no hay o no se puede leer.</summary>
        public static ArchivoSincro Cargar(string ruta)
        {
            try
            {
                var archivo = JsonSerializer.Deserialize<ArchivoSincro>(File.ReadAllText(ruta));
                if (archivo == null)
                    return new ArchivoSincro();
                if (string.IsNullOrWhiteSpace(archivo.IdiomaEdicion))
                    archivo.IdiomaEdicion = IdiomaPorDefecto;
                archivo.Excluyentes = (archivo.Excluyentes ?? new List<GrupoGuardado>()).Where(g => g?.Senales != null).ToList();
                return archivo;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return new ArchivoSincro();
            }
        }

        public void Guardar(string ruta)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
