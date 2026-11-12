using HerramientasSICAR.Core.Sicar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logic
{
    /// <summary>
    /// sincro.json, junto al programa exportado de un PLC: el idioma de edición con el que se leen los nombres de los
    /// pasos y las señales que el usuario ha marcado como excluyentes (las decisiones de Sincro Checker).
    /// </summary>
    public sealed class SyncFile
    {
        public const string Name = "sincro.json";

        /// <summary>Idioma de edición que se usa cuando no se sabe (carpeta exportada con otra herramienta).</summary>
        public const string DefaultLanguage = "en-US";

        [JsonPropertyName("IdiomaEdicion")]
        public string EditingLanguage { get; set; } = DefaultLanguage;

        [JsonPropertyName("Excluyentes")]
        public List<SavedGroup> Exclusive { get; set; } = new List<SavedGroup>();

        public sealed class SavedGroup
        {
            [JsonPropertyName("Senales")]
            public List<string> Signals { get; set; } = new List<string>();

            /// <summary>Valor de cada señal; sin él (archivos anteriores), todas a 1.</summary>
            [JsonPropertyName("Valores")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<bool> Values { get; set; }

            [JsonPropertyName("Nota")]
            public string Note { get; set; }
        }

        public List<ExclusiveGroup> Groups() =>
            Exclusive.Select(g => new ExclusiveGroup(g.Signals) { Note = g.Note, Values = g.Values }).ToList();

        public void SetGroups(IEnumerable<ExclusiveGroup> groups) =>
            Exclusive = groups.Select(g => new SavedGroup
            {
                Signals = g.Signals.ToList(),
                Values = g.Values == null || g.Values.All(v => v) ? null : g.Values.ToList(),
                Note = g.Note,
            }).ToList();

        /// <summary>sincro.json dentro de una carpeta exportada a mano (la que se abre con «Abrir carpeta»).</summary>
        public static string InFolder(string folder) => Path.Combine(folder, Name);

        /// <summary>
        /// Archivo de un PLC exportado desde TIA: %AppData%\HerramientasSICAR\Sincro\&lt;proyecto&gt;\&lt;PLC&gt;.json, para no
        /// dejar nada en la carpeta del proyecto.
        /// </summary>
        public static string InAppData(string project, string plc) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HerramientasSICAR", "Sincro",
                FileRules.SafeName(project), FileRules.SafeName(plc) + ".json");

        /// <summary>El de esa ruta, o uno vacío si no hay o no se puede leer.</summary>
        public static SyncFile Load(string path)
        {
            try
            {
                var file = JsonSerializer.Deserialize<SyncFile>(File.ReadAllText(path));
                if (file == null)
                    return new SyncFile();
                if (string.IsNullOrWhiteSpace(file.EditingLanguage))
                    file.EditingLanguage = DefaultLanguage;
                file.Exclusive = (file.Exclusive ?? new List<SavedGroup>()).Where(g => g?.Signals != null).ToList();
                return file;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return new SyncFile();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
