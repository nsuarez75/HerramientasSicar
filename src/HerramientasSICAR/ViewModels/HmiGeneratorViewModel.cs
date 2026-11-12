using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logic;
using HerramientasSICAR.Logic.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Generador HMI: se configuran los menús de las HMI de un PLC (nombres de pantallas, qué pantallas ve cada HMI y
    /// funciones de usuario) y se generan el FC CallHMIMenusConfig_FC y las listas de textos en XML para importar en TIA.
    /// La configuración se guarda en un archivo .hmimenus.
    /// </summary>
    public partial class HmiGeneratorViewModel : Tool
    {
        private MenuProject _project;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FileName))]
        private string _projectPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FileName))]
        private bool _changed;

        [ObservableProperty]
        private EditingLanguage _language;

        /// <summary>Nodo elegido en el árbol (grupo o subgrupo).</summary>
        [ObservableProperty]
        private MenuNode _selectedNode;

        /// <summary>Subgrupo cuya tabla se ve: el último subgrupo elegido en el árbol.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOem), nameof(SubgroupNote), nameof(HasSubgroupNote), nameof(HasNames), nameof(HasCheckBoxes))]
        private MenuSubgroup _currentSubgroup;

        [ObservableProperty]
        private FunctionScreen _selectedScreen;

        [ObservableProperty]
        private ProductionDataset _selectedDataset;

        [ObservableProperty]
        private EditableText _oemTitle;

        [ObservableProperty]
        private IReadOnlyList<ScreenRow> _rows = new List<ScreenRow>();

        public HmiGeneratorViewModel(ToolContext context) : base(context)
        {
            Groups = MenuCatalog.Subgroups.GroupBy(s => s.Group).Select(g => MenuNode.FromGroup(g.Key, g)).ToList();

            string last = ToolContext.Settings.LastHmiProject;
            MenuProject project = null;
            if (!string.IsNullOrEmpty(last) && File.Exists(last))
            {
                try
                {
                    project = MenuProject.Open(last);
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    project = null;
                }
            }
            Load(project ?? MenuProject.New(), project == null ? null : last);

            ImportGroups = new[]
            {
                new ImportGroup(L.T("Bloque"), new[] { MenuGenerator.FcName }),
                new ImportGroup(L.T("Listas de menús y funciones de usuario"),
                    MenuCatalog.Lists.Select(l => l.Name).Concat(new[] { MenuCatalog.FunctionTextsList })),
                new ImportGroup(L.T("Listas de Production data"),
                    MenuCatalog.ProductionDataKinds.SelectMany(t => Enumerable.Range(1, MenuCatalog.ProductionDatasets).Select(t.ListName))),
            };
            foreach (var component in ImportGroups.SelectMany(g => g.Components))
                component.PropertyChanged += (_, __) => ImportIntoTiaCommand.NotifyCanExecuteChanged();
            ToolContext.TreeChanged += change => { if (change.IsFull) SetTargets(); };
            SetTargets();
        }

        public override string Name => L.T("Generador HMI");
        public override string Description => L.T("Configura los menús de las HMI del PLC (nombres de pantallas, qué pantallas ve cada HMI y funciones de usuario) y genera el FC CallHMIMenusConfig_FC y las listas de textos en XML para importarlos en TIA.");
        public override string Icon => "monitor";
        public override bool UsesTia => false;

        public IReadOnlyList<MenuNode> Groups { get; }

        // ------------------------------------------------------------------ Importar en TIA

        /// <summary>Pestaña que se ve (0 Pantallas, 1 Funciones de usuario, 2 Production data, 3 Importar en TIA).</summary>
        [ObservableProperty]
        private int _tab;

        public IReadOnlyList<ImportGroup> ImportGroups { get; }

        /// <summary>PLC del proyecto conectado, para el FC.</summary>
        public ObservableCollection<string> Plcs { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ImportIntoTiaCommand))]
        private string _targetPlc;

        /// <summary>HMI del proyecto conectado, para las listas de textos.</summary>
        public ObservableCollection<HmiTarget> TargetHmis { get; } = new ObservableCollection<HmiTarget>();

        public bool HasDevices => Plcs.Count > 0 || TargetHmis.Count > 0;

        // PLC y HMI del proyecto conectado (se rehacen al conectar). Se conserva lo elegido si sigue existiendo.
        private void SetTargets()
        {
            string plc = TargetPlc;
            var checkedHmis = TargetHmis.Where(h => h.Checked).Select(h => h.Name).ToList();
            bool firstTime = TargetHmis.Count == 0;

            Plcs.Clear();
            TargetHmis.Clear();
            foreach (var device in ToolContext.Devices)
            {
                if (device.Kind == DeviceKind.Hmi)
                {
                    var target = new HmiTarget(device.Name, ToolContext.DisplayName(device.Name),
                        firstTime || checkedHmis.Contains(device.Name));
                    target.PropertyChanged += (_, __) => ImportIntoTiaCommand.NotifyCanExecuteChanged();
                    TargetHmis.Add(target);
                }
                else
                    Plcs.Add(device.Name);
            }
            TargetPlc = Plcs.Contains(plc) ? plc : Plcs.FirstOrDefault();
            OnPropertyChanged(nameof(HasDevices));
            ImportIntoTiaCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void GoToImport() => Tab = 3;

        /// <summary>
        /// Genera en una carpeta temporal e importa lo marcado: el FC en el PLC (en su carpeta si ya está, si no en
        /// 08_HMI_Management › 01_HMI_Configuration) y las listas en cada HMI marcada. Borra la carpeta al terminar.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanImportIntoTia))]
        private async Task ImportIntoTia()
        {
            var errors = _project.Errors();
            if (errors.Count > 0)
            {
                await ToolContext.Dialogs.MessageNetwork(L.T("No se puede generar"), string.Join("\n", errors));
                return;
            }

            bool fc = ImportGroups[0].Components[0].Checked;
            var lists = ImportGroups.Skip(1).SelectMany(g => g.Components).Where(c => c.Checked).Select(c => c.Name).ToList();
            var hmis = TargetHmis.Where(h => h.Checked).Select(h => h.Name).ToList();
            if (lists.Count == 0)
                hmis.Clear();
            string plc = fc ? TargetPlc : null;

            var parts = new List<string>();
            if (plc != null)
                parts.Add(L.T("el FC {0} en {1}", MenuGenerator.FcName, plc));
            if (hmis.Count > 0)
                parts.Add(L.T("{0} lista(s) de textos en {1}", lists.Count, string.Join(", ", hmis.Select(ToolContext.DisplayName))));
            if (!await ToolContext.Dialogs.Confirm(L.T("Importar en TIA"),
                    L.T("Se importan {0}. Lo que ya exista con el mismo nombre se sobrescribe. Guarda antes el proyecto en TIA.", string.Join(L.T(" y "), parts)),
                    L.T("Importar")))
                return;

            var project = _project;
            await ToolContext.Work(L.T("Generando…"), async () =>
            {
                var tia = await ToolContext.TiaAsync();
                var progress = ToolContext.TiaProgress();
                int imported = 0;
                var failures = new List<string>();
                using (var temp = new TempFolder())
                {
                    await Task.Run(() => MenuGenerator.Generate(project, temp.Path));

                    if (plc != null)
                    {
                        // En su carpeta si el PLC ya tiene el FC (sobrescribiéndolo); si no, en la de la plantilla.
                        var r = await tia.ImportBlocksAsync(new ImportBlocksArgs
                        {
                            Files =
                            {
                                new ImportFile
                                {
                                    Path = Path.Combine(temp.Path, MenuGenerator.FcName + ".xml"), Plc = plc,
                                    Folders = FcFolder.ToList(), UseExistingFolder = true
                                }
                            }
                        }, progress);
                        imported += r.Succeeded;
                        failures.AddRange(TiaTexts.Failures(r, importing: true));
                    }

                    var files = lists.Select(l => new HmiImportFile
                    {
                        Path = Path.Combine(temp.Path, MenuGenerator.ListsFolder, l + ".xml"), Branch = HmiBranch.TextLists
                    }).ToList();
                    foreach (string hmi in hmis)
                    {
                        var r = await tia.ImportHmiAsync(new ImportHmiArgs { Hmi = hmi, Files = files }, progress);
                        imported += r.Succeeded;
                        failures.AddRange(TiaTexts.Failures(r).Select(f => ToolContext.DisplayName(hmi) + " · " + f));
                    }
                }

                // El FC nuevo se ve en el árbol común si ese PLC ya estaba leído.
                if (plc != null && ToolContext.ReadPlc(plc) != null)
                    await ToolContext.ReadPlcAsync(plc);

                ToolContext.SetFailures(failures);
                string message = L.T("{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos.", imported);
                ToolContext.ShowStatus(failures.Count == 0 ? message : message + " " + L.T("{0} con error (ver abajo).", failures.Count),
                    error: failures.Count > 0);
            });
        }

        /// <summary>Carpeta del FC si el PLC aún no lo tiene (la de la plantilla SICAR@GST).</summary>
        private static readonly IReadOnlyList<string> FcFolder = new[] { "08_HMI_Management", "01_HMI_Configuration" };

        private bool CanImportIntoTia()
        {
            if (!CanUseTia)
                return false;
            bool fc = ImportGroups[0].Components[0].Checked && TargetPlc != null;
            bool lists = ImportGroups.Skip(1).Any(g => g.Components.Any(c => c.Checked)) && TargetHmis.Any(h => h.Checked);
            return fc || lists;
        }
        public ObservableCollection<EditableHmi> Hmis { get; } = new ObservableCollection<EditableHmi>();
        public ObservableCollection<FunctionScreen> FunctionScreens { get; } = new ObservableCollection<FunctionScreen>();
        public ObservableCollection<ProductionDataset> ProductionDatasets { get; } = new ObservableCollection<ProductionDataset>();

        public IReadOnlyList<string> Languages { get; } = new[]
        {
            L.T("Texto (para todos los idiomas)"), L.T("Inglés (si es distinto)"), L.T("Alemán (si es distinto)")
        };

        public int LanguageIndex
        {
            get => (int)Language;
            set => Language = (EditingLanguage)Math.Max(0, value);
        }

        public string FileName =>
            (ProjectPath == null ? L.T("Proyecto sin guardar") : Path.GetFileName(ProjectPath)) + (Changed ? " *" : "");

        public bool IsOem => CurrentSubgroup?.Group == MenuCatalog.Oem;
        public bool HasNames => CurrentSubgroup?.Names > 0;
        public bool HasCheckBoxes => CurrentSubgroup?.Positions > 0;

        public string SubgroupNote
        {
            get
            {
                var s = CurrentSubgroup;
                if (s == null)
                    return "";
                if (s.Group == MenuCatalog.ProductionData)
                    return L.T("Cada posición es un dataset; sus textos se escriben en la pestaña Production data.");
                if (s.Positions == 0)
                    return L.T("Solo nombres para la lista {0}: el FC no oculta estas pantallas.", s.TargetList.Name);
                if (s.Names == 0)
                    return L.T("Sin lista de textos: solo se elige qué posiciones se ven en cada HMI.");
                string list = s.TargetList.Name + (s.Offset > 0 ? L.T(" (valores {0} a {1})", s.Offset + 1, s.Offset + s.Names) : "");
                return L.T("Nombres en la lista {0}. Si una pantalla no tiene nombre, sale el texto estándar.", list);
            }
        }

        public bool HasSubgroupNote => CurrentSubgroup != null;

        // ------------------------------------------------------------------ Proyecto

        private void Load(MenuProject project, string path)
        {
            _project = project;
            ProjectPath = path;

            Hmis.Clear();
            foreach (var hmi in project.Hmis.OrderBy(h => h.Number))
                Hmis.Add(new EditableHmi(hmi, this));

            FunctionScreens.Clear();
            for (int p = 1; p <= MenuCatalog.FunctionScreens; p++)
            {
                int screen = p;
                var functions = project.UserFunctionScreens[p - 1].Select((f, i) => new FunctionRow(i + 1, f, this, screen)).ToList();
                FunctionScreens.Add(new FunctionScreen(p, project, functions));
            }

            ProductionDatasets.Clear();
            foreach (var tipo in MenuCatalog.ProductionDataKinds)
                for (int dataset = 1; dataset <= MenuCatalog.ProductionDatasets; dataset++)
                    ProductionDatasets.Add(new ProductionDataset(tipo, dataset, project, this));
            SelectedDataset = ProductionDatasets[0];

            if (SelectedNode == null)
                SelectedNode = Groups[0].Children[0];
            else
                RebuildRows();
            SelectedScreen = FunctionScreens[0];
            Changed = false;
        }

        // Al elegir un grupo la tabla sigue con el último subgrupo; al elegir un subgrupo, se cambia a él.
        partial void OnSelectedNodeChanged(MenuNode value)
        {
            if (value?.Subgroup != null)
                CurrentSubgroup = value.Subgroup;
        }

        partial void OnCurrentSubgroupChanged(MenuSubgroup value) => RebuildRows();

        partial void OnLanguageChanged(EditingLanguage value)
        {
            OnPropertyChanged(nameof(LanguageIndex));
            foreach (var row in Rows.Where(f => f.Name != null))
                row.Name.Refresh();
            OemTitle?.Refresh();
            foreach (var text in FunctionScreens.SelectMany(p => p.Functions).SelectMany(f => f.Texts))
                text.Refresh();
            foreach (var row in ProductionDatasets.SelectMany(d => d.Rows))
                row.Text.Refresh();
        }

        private void RebuildRows()
        {
            var subgroup = CurrentSubgroup;
            if (subgroup == null)
            {
                Rows = new List<ScreenRow>();
                OemTitle = null;
                return;
            }

            OemTitle = subgroup.Group == MenuCatalog.Oem
                ? new EditableText(_project.OemTitles[subgroup.Id - 1], this, () => $"OEM_{subgroup.Id:D2}")
                : null;

            var rows = new List<ScreenRow>();
            for (int position = 1; position <= subgroup.Rows; position++)
            {
                int p = position;
                EditableText name = null;
                if (position <= subgroup.Names)
                    name = new EditableText(_project.Name(subgroup, position), this, () => StandardText(subgroup, p));
                var checkBoxes = Hmis.Select(h => new CheckBoxVisible(
                    position <= subgroup.Positions ? h.Hmi.VisibleScreens[subgroup.Key] : null, position - 1, this)).ToList();
                rows.Add(new ScreenRow(position, name, checkBoxes));
            }
            Rows = rows;
        }

        private string StandardText(MenuSubgroup subgroup, int position)
        {
            int language = Language == EditingLanguage.English ? 1 : Language == EditingLanguage.German ? 2 : 0;
            if (subgroup.Group == MenuCatalog.Oem)
                return $"<<OEM {subgroup.Id:D2} screen {position:D2}>>";
            return subgroup.TargetList.StandardText(language, subgroup.Offset + position);
        }

        /// <summary>Algo del proyecto ha cambiado (lo llaman las filas al editar).</summary>
        public void MarkChanged()
        {
            Changed = true;
            foreach (var screen in FunctionScreens)
                screen.Refresh();
            if (SelectedScreen != null)
                foreach (var text in SelectedScreen.Functions.SelectMany(f => f.Texts))
                    text.RefreshCheck();
            foreach (var dataset in ProductionDatasets)
                dataset.Refresh();
            if (SelectedDataset != null)
                foreach (var row in SelectedDataset.Rows)
                    row.Text.RefreshCheck();
        }

        private static FileKind ProjectFileKind => new FileKind(L.T("Proyecto del Generador HMI (*.hmimenus)"), "*" + MenuProject.Extension);

        // Si hay cambios sin guardar, pregunta antes de perderlos.
        private async Task<bool> CanDiscard() =>
            !Changed || await ToolContext.Dialogs.Confirm(L.T("Cambios sin guardar"),
                L.T("El proyecto tiene cambios sin guardar. ¿Descartarlos?"), L.T("Descartar"));

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task New()
        {
            if (!await CanDiscard())
                return;
            Load(MenuProject.New(), null);
            ToolContext.ShowStatus(L.T("Proyecto nuevo con una HMI."));
        }

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task Open()
        {
            if (!await CanDiscard())
                return;
            string path = await ToolContext.Dialogs.ChooseFile(L.T("Abrir un proyecto del Generador HMI"), InitialFolder(), ProjectFileKind);
            if (path == null)
                return;
            try
            {
                Load(MenuProject.Open(path), path);
                RememberProject(path);
                ToolContext.ShowStatus(L.T("Proyecto abierto: {0}", path));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                ToolContext.ShowStatus(L.T("Error: {0}", e.Message), error: true);
            }
        }

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task Save()
        {
            if (ProjectPath == null)
                await SaveAs();
            else
                SaveTo(ProjectPath);
        }

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task SaveAs()
        {
            string suggested = ProjectPath ?? Path.Combine(InitialFolder() ?? "", "Menus HMI" + MenuProject.Extension);
            string path = await ToolContext.Dialogs.SaveFile(L.T("Guardar el proyecto como…"), suggested, ProjectFileKind);
            if (path != null)
                SaveTo(path);
        }

        private void SaveTo(string path)
        {
            try
            {
                _project.Save(path);
                ProjectPath = path;
                Changed = false;
                RememberProject(path);
                ToolContext.ShowStatus(L.T("Proyecto guardado: {0}", path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                ToolContext.ShowStatus(L.T("Error: {0}", e.Message), error: true);
            }
        }

        private void RememberProject(string path)
        {
            ToolContext.Settings.LastHmiProject = path;
            ToolContext.Settings.LastFolder = Path.GetDirectoryName(path);
            ToolContext.Settings.Save();
        }

        private string InitialFolder() =>
            ProjectPath != null ? Path.GetDirectoryName(ProjectPath) : ToolContext.Settings.LastFolder;

        // ------------------------------------------------------------------ HMIs

        [RelayCommand(CanExecute = nameof(Idle))]
        private void AddHmi()
        {
            if (_project.AddHmi() == null)
            {
                ToolContext.ShowStatus(L.T("Ya hay 8 HMI, el máximo (hay 8 IDB HMIMenusConfig)."), error: true);
                return;
            }
            ReloadHmis();
        }

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task RemoveHmi(EditableHmi hmi)
        {
            if (hmi == null || !await ToolContext.Dialogs.Confirm(L.T("Quitar HMI"),
                    L.T("¿Quitar la HMI {0} del proyecto? Se pierde qué pantallas se ven en ella.", hmi.Name), L.T("Quitar")))
                return;
            _project.Hmis.Remove(hmi.Hmi);
            ReloadHmis();
        }

        private void ReloadHmis()
        {
            Hmis.Clear();
            foreach (var hmi in _project.Hmis.OrderBy(h => h.Number))
                Hmis.Add(new EditableHmi(hmi, this));
            RebuildRows();
            MarkChanged();
        }

        /// <summary>Marca o desmarca todas las pantallas del subgrupo elegido en una HMI.</summary>
        [RelayCommand]
        private void ToggleColumn(EditableHmi hmi)
        {
            var subgroup = CurrentSubgroup;
            if (hmi == null || subgroup == null || subgroup.Positions == 0)
                return;
            var visibleScreens = hmi.Hmi.VisibleScreens[subgroup.Key];
            bool check = visibleScreens.Any(v => !v);
            for (int i = 0; i < visibleScreens.Count; i++)
                visibleScreens[i] = check;
            foreach (var row in Rows)
                foreach (var checkBox in row.CheckBoxes)
                    checkBox.Refresh();
            MarkChanged();
        }

        // ------------------------------------------------------------------ Traducir y generar

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task Translate()
        {
            var texts = AllTexts().Where(t => !string.IsNullOrWhiteSpace(t.Text)
                && (string.IsNullOrWhiteSpace(t.English) || string.IsNullOrWhiteSpace(t.German))).ToList();
            if (texts.Count == 0)
            {
                ToolContext.ShowStatus(L.T("Todos los textos tienen ya su inglés y su alemán."));
                return;
            }
            if (!await ToolContext.Dialogs.Confirm(L.T("Traducir"),
                    L.T("Se rellenan el inglés y el alemán de {0} textos que no los tienen, con un traductor en línea (necesita internet). Revísalos después eligiendo el idioma arriba.", texts.Count),
                    L.T("Traducir")))
                return;

            await ToolContext.Work(L.T("Traduciendo…"), async () =>
            {
                var pairList = texts.SelectMany(t => new[]
                {
                    string.IsNullOrWhiteSpace(t.English) ? (t.Text.Trim(), "en") : default,
                    string.IsNullOrWhiteSpace(t.German) ? (t.Text.Trim(), "de") : default
                }).Where(pair => pair.Item1 != null);
                var translations = await Translator.TranslateManyAsync(pairList);

                // Una traducción igual al original (nombres de equipos, o fallo del traductor) se deja vacía: sale el texto.
                string TranslationOf(string source, string target) =>
                    translations.TryGetValue((source, target), out var t) && !string.Equals(t.Trim(), source, StringComparison.OrdinalIgnoreCase) ? t.Trim() : null;
                foreach (var text in texts)
                {
                    string source = text.Text.Trim();
                    if (string.IsNullOrWhiteSpace(text.English))
                        text.English = TranslationOf(source, "en") ?? "";
                    if (string.IsNullOrWhiteSpace(text.German))
                        text.German = TranslationOf(source, "de") ?? "";
                }
                OnLanguageChanged(Language);
                MarkChanged();
                ToolContext.ShowStatus(L.T("Traducidos {0} textos. Revísalos eligiendo Inglés o Alemán arriba.", texts.Count));
            });
        }

        private IEnumerable<HmiText> AllTexts() =>
            _project.OemTitles
                .Concat(_project.Names.Values.SelectMany(n => n))
                .Concat(_project.UserFunctionScreens.SelectMany(p => p).SelectMany(f => f.Texts()))
                .Concat(_project.ProductionData.Values.SelectMany(d => d).SelectMany(d => d));

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task Generate()
        {
            var errors = _project.Errors();
            if (errors.Count > 0)
            {
                await ToolContext.Dialogs.MessageNetwork(L.T("No se puede generar"), string.Join("\n", errors));
                return;
            }
            string folder = await ToolContext.Dialogs.ChooseFolder(L.T("Carpeta donde generar el FC y las listas de textos"),
                ToolContext.Settings.LastHmiOutputFolder ?? InitialFolder());
            if (folder == null)
                return;
            ToolContext.Settings.LastHmiOutputFolder = folder;
            ToolContext.Settings.Save();

            var project = _project;
            await ToolContext.Work(L.T("Generando…"), async () =>
            {
                var written = await Task.Run(() => MenuGenerator.Generate(project, folder));
                ToolContext.ShowStatus(L.T("Generados el FC y {0} listas de textos en {1}", written.Count - 1, folder));
            });
        }

        protected override void UpdateCommands()
        {
            NewCommand.NotifyCanExecuteChanged();
            OpenCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsCommand.NotifyCanExecuteChanged();
            AddHmiCommand.NotifyCanExecuteChanged();
            RemoveHmiCommand.NotifyCanExecuteChanged();
            TranslateCommand.NotifyCanExecuteChanged();
            GenerateCommand.NotifyCanExecuteChanged();
            ImportIntoTiaCommand?.NotifyCanExecuteChanged();
        }
    }
}
