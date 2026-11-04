using HerramientasSICAR.Nucleo.Sicar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

        /// <summary>El de la carpeta, o uno vacío si no hay o no se puede leer.</summary>
        public static ArchivoSincro Cargar(string carpeta)
        {
            try
            {
                var archivo = JsonSerializer.Deserialize<ArchivoSincro>(File.ReadAllText(Path.Combine(carpeta, Nombre)));
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

        public void Guardar(string carpeta) =>
            File.WriteAllText(Path.Combine(carpeta, Nombre), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
