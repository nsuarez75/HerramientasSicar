using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HerramientasSICAR.Logic.Menus
{
    /// <summary>
    /// Texto de una entrada de lista de textos. <see cref="Texto"/> va a los tres idiomas; <see cref="Ingles"/> y
    /// <see cref="Aleman"/>, si se rellenan (a mano o con «Traducir»), sustituyen a Texto en en-US y de-DE.
    /// </summary>
    public sealed class HmiText
    {
        [JsonPropertyName("Texto")]
        public string Text { get; set; } = "";
        [JsonPropertyName("Ingles")]
        public string English { get; set; } = "";
        [JsonPropertyName("Aleman")]
        public string German { get; set; } = "";

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Text) && string.IsNullOrWhiteSpace(English) && string.IsNullOrWhiteSpace(German);

        /// <summary>Texto en es-ES (0), en-US (1) o de-DE (2).</summary>
        public string InLanguage(int language)
        {
            string own = language == 1 ? English : language == 2 ? German : null;
            return string.IsNullOrWhiteSpace(own) ? (Text ?? "").Trim() : own.Trim();
        }

        public static HmiText From(string text) => new HmiText { Text = text ?? "" };
    }

    /// <summary>Una función de usuario: cinco textos en CO_UserFunctions.</summary>
    public sealed class UserFunction
    {
        [JsonPropertyName("Nombre")]
        public HmiText Name { get; set; } = new HmiText();
        [JsonPropertyName("AccionIzquierda")]
        public HmiText LeftAction { get; set; } = new HmiText();
        [JsonPropertyName("AccionDerecha")]
        public HmiText RightAction { get; set; } = new HmiText();
        [JsonPropertyName("FeedbackIzquierda")]
        public HmiText LeftFeedback { get; set; } = new HmiText();
        [JsonPropertyName("FeedbackDerecha")]
        public HmiText RightFeedback { get; set; } = new HmiText();

        /// <summary>Los cinco textos en el orden de la lista (valores N1 a N5).</summary>
        public IReadOnlyList<HmiText> Texts() => new[] { Name, LeftAction, RightAction, LeftFeedback, RightFeedback };

        [JsonIgnore]
        public bool IsEmptyFunction => Texts().All(t => t.IsEmpty);
    }

    /// <summary>Una HMI del PLC: su nombre, el número de su IDB y qué pantallas se ven en ella.</summary>
    public sealed class HmiMenus
    {
        [JsonPropertyName("Nombre")]
        public string Name { get; set; } = "";

        /// <summary>Número del IDB HMIMenusConfigNN_IDB (1 a 8).</summary>
        [JsonPropertyName("Numero")]
        public int Number { get; set; }

        /// <summary>Por subgrupo (clave), si se ve cada posición (índice 0 = posición 1).</summary>
        [JsonPropertyName("Visibles")]
        public Dictionary<string, List<bool>> VisibleScreens { get; set; } = new Dictionary<string, List<bool>>();

        public bool Visible(MenuSubgroup subgroup, int position) =>
            VisibleScreens.TryGetValue(subgroup.Key, out var list) && position >= 1 && position <= list.Count && list[position - 1];

        [JsonIgnore]
        public string IdbName => $"HMIMenusConfig{Number:D2}_IDB";
    }

    /// <summary>
    /// Configuración de los menús de las HMI de un PLC: lo que guarda el Generador HMI en su archivo .hmimenus.
    /// </summary>
    public sealed class MenuProject
    {
        public const string Extension = ".hmimenus";

        public int Version { get; set; } = 1;

        public List<HmiMenus> Hmis { get; set; } = new List<HmiMenus>();

        /// <summary>Títulos de los grupos OEM 1 a 8 (valores 10, 20… de CO_OEM_Screens_ID).</summary>
        [JsonPropertyName("TitulosOem")]
        public List<HmiText> OemTitles { get; set; } = new List<HmiText>();

        /// <summary>Por subgrupo (clave), nombre de cada pantalla (índice 0 = posición 1). Comunes a todas las HMI.</summary>
        [JsonPropertyName("Nombres")]
        public Dictionary<string, List<HmiText>> Names { get; set; } = new Dictionary<string, List<HmiText>>();

        /// <summary>Las 24 pantallas de funciones de usuario, con 8 funciones cada una.</summary>
        [JsonPropertyName("FuncionesUsuario")]
        public List<List<UserFunction>> UserFunctionScreens { get; set; } = new List<List<UserFunction>>();

        /// <summary>Por tipo de Production data (clave), los 8 datasets con los textos de sus filas.</summary>
        [JsonPropertyName("DatosProduccion")]
        public Dictionary<string, List<List<HmiText>>> ProductionData { get; set; } = new Dictionary<string, List<List<HmiText>>>();

        /// <summary>Textos de las filas de un dataset de Production data (dataset de 1 a 8).</summary>
        public List<HmiText> Dataset(ProductionDataKind tipo, int dataset) => ProductionData[tipo.Key][dataset - 1];

        /// <summary>Proyecto vacío con una HMI.</summary>
        public static MenuProject New()
        {
            var project = new MenuProject();
            project.Complete();
            project.AddHmi();
            return project;
        }

        /// <summary>Nombre de una pantalla (vacío si no tiene).</summary>
        public HmiText Name(MenuSubgroup subgroup, int position) =>
            Names.TryGetValue(subgroup.Key, out var list) && position >= 1 && position <= list.Count ? list[position - 1] : new HmiText();

        /// <summary>Añade una HMI con el primer número libre y la visibilidad por defecto; null si ya hay 8.</summary>
        public HmiMenus AddHmi()
        {
            int number = Enumerable.Range(1, MenuCatalog.MaxHmis).FirstOrDefault(n => Hmis.All(h => h.Number != n));
            if (number == 0)
                return null;
            var hmi = new HmiMenus { Name = $"HMI{number:D2}", Number = number };
            Hmis.Add(hmi);
            Complete();
            Hmis.Sort((a, b) => a.Number.CompareTo(b.Number));
            return hmi;
        }

        /// <summary>Rellena lo que falte (archivo antiguo o a medias) para que todas las listas tengan su tamaño.</summary>
        public void Complete()
        {
            Hmis = Hmis ?? new List<HmiMenus>();
            OemTitles = Fit(OemTitles, MenuCatalog.OemGroups, _ => new HmiText());
            Names = Names ?? new Dictionary<string, List<HmiText>>();
            foreach (var subgroup in MenuCatalog.Subgroups.Where(s => s.Names > 0))
            {
                Names.TryGetValue(subgroup.Key, out var names);
                Names[subgroup.Key] = Fit(names, subgroup.Names, _ => new HmiText());
            }

            UserFunctionScreens = Fit(UserFunctionScreens, MenuCatalog.FunctionScreens, _ => new List<UserFunction>());
            for (int i = 0; i < UserFunctionScreens.Count; i++)
                UserFunctionScreens[i] = Fit(UserFunctionScreens[i], MenuCatalog.FunctionsPerScreen, _ => new UserFunction());
            foreach (var function in UserFunctionScreens.SelectMany(p => p))
            {
                function.Name = function.Name ?? new HmiText();
                function.LeftAction = function.LeftAction ?? new HmiText();
                function.RightAction = function.RightAction ?? new HmiText();
                function.LeftFeedback = function.LeftFeedback ?? new HmiText();
                function.RightFeedback = function.RightFeedback ?? new HmiText();
            }

            ProductionData = ProductionData ?? new Dictionary<string, List<List<HmiText>>>();
            foreach (var tipo in MenuCatalog.ProductionDataKinds)
            {
                ProductionData.TryGetValue(tipo.Key, out var datasets);
                datasets = Fit(datasets, MenuCatalog.ProductionDatasets, _ => new List<HmiText>());
                for (int i = 0; i < datasets.Count; i++)
                    datasets[i] = Fit(datasets[i], tipo.Rows, _ => new HmiText());
                ProductionData[tipo.Key] = datasets;
            }

            foreach (var hmi in Hmis)
            {
                hmi.VisibleScreens = hmi.VisibleScreens ?? new Dictionary<string, List<bool>>();
                foreach (var subgroup in MenuCatalog.Subgroups.Where(s => s.Positions > 0))
                {
                    hmi.VisibleScreens.TryGetValue(subgroup.Key, out var visibleScreens);
                    hmi.VisibleScreens[subgroup.Key] = Fit(visibleScreens, subgroup.Positions, i => MenuCatalog.VisibleByDefault(subgroup, i + 1));
                }
            }
        }

        /// <summary>Errores que impiden generar (vacío si se puede).</summary>
        public IReadOnlyList<string> Errors()
        {
            var errors = new List<string>();
            if (Hmis.Count == 0)
                errors.Add(L.T("No hay ninguna HMI."));
            foreach (var repeated in Hmis.GroupBy(h => h.Number).Where(g => g.Count() > 1))
                errors.Add(L.T("Hay varias HMI con el número {0}.", repeated.Key));
            foreach (var hmi in Hmis.Where(h => h.Number < 1 || h.Number > MenuCatalog.MaxHmis))
                errors.Add(L.T("El número de la HMI {0} tiene que ir del 1 al 8.", hmi.Name));
            foreach (var hmi in Hmis.Where(h => string.IsNullOrWhiteSpace(h.Name)))
                errors.Add(L.T("La HMI {0} no tiene nombre.", hmi.Number));
            return errors;
        }

        private static List<T> Fit<T>(List<T> list, int size, Func<int, T> created)
        {
            list = list ?? new List<T>();
            for (int i = 0; i < list.Count; i++)
                if (list[i] == null)
                    list[i] = created(i);
            while (list.Count < size)
                list.Add(created(list.Count));
            if (list.Count > size)
                list.RemoveRange(size, list.Count - size);
            return list;
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static MenuProject FromJson(string json)
        {
            var project = JsonSerializer.Deserialize<MenuProject>(json, JsonOptions)
                ?? throw new InvalidDataException(L.T("El archivo no es un proyecto del Generador HMI."));
            project.Complete();
            return project;
        }

        public void Save(string path) => File.WriteAllText(path, ToJson(), new UTF8Encoding(false));

        public static MenuProject Open(string path)
        {
            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                throw new InvalidDataException(L.T("El archivo no es un proyecto del Generador HMI."));
            }
        }
    }
}
