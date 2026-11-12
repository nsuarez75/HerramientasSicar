using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ImportExportViewModel _importExport;

        public MainViewModel(Settings settings)
        {
            ToolContext = new ToolContext(settings);
            ToolContext.BusyChanged += () =>
            {
                FindProjectsCommand.NotifyCanExecuteChanged();
                ConnectCommand.NotifyCanExecuteChanged();
            };

            _importExport = new ImportExportViewModel(ToolContext);
            // Todas trabajan con el proyecto conectado; el Generador HMI también edita y genera sin conexión.
            foreach (var tool in new Tool[]
            {
                _importExport,
                new HmiGeneratorViewModel(ToolContext),
                new CommentatorViewModel(ToolContext),
                new SyncCheckerViewModel(ToolContext),
                new DiagExpectedViewModel(ToolContext),
                new MessageNumberingViewModel(ToolContext),
                new RenumberArrayViewModel(ToolContext),
            })
                Sidebar.Add(tool);

            _selectedTool = _current = _importExport;
            _sidebarHidden = settings.SidebarHidden;
        }

        /// <summary>
        /// Lateral de herramientas oculto: se gana sitio para la herramienta, que se cambia con el desplegable de la
        /// cabecera. Se recuerda.
        /// </summary>
        [ObservableProperty]
        private bool _sidebarHidden;

        partial void OnSidebarHiddenChanged(bool value)
        {
            ToolContext.Settings.SidebarHidden = value;
            ToolContext.Settings.Save();
        }

        [RelayCommand]
        private void ToggleSidebar() => SidebarHidden = !SidebarHidden;

        public ToolContext ToolContext { get; }

        public IDialogs Dialogs
        {
            get => ToolContext.Dialogs;
            set => ToolContext.Dialogs = value;
        }

        /// <summary>El tema cambió: la aplicación lo aplica.</summary>
        public event Action ThemeChanged;

        /// <summary>El idioma cambió: los textos se traducen al cargar las vistas, así que se monta otra ventana.</summary>
        public event Action LanguageChanged;

        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version;
        public string Title => L.T("Herramientas SICAR") + " " + CurrentVersion.ToString(3);
        public string AppName => L.T("Herramientas SICAR");
        public string Subtitle => L.T("Utilidades para proyectos SICAR@GST en TIA Portal");

        /// <summary>Rótulos de grupo y herramientas, en el orden en que se ven en el lateral.</summary>
        public ObservableCollection<Tool> Sidebar { get; } = new ObservableCollection<Tool>();

        public ObservableCollection<string> Projects { get; } = new ObservableCollection<string>();

        /// <summary>Lo seleccionado en el lateral. Los rótulos de grupo están desactivados, pero si llegan aquí no cambian la herramienta.</summary>
        [ObservableProperty]
        private object _selectedTool;

        /// <summary>Herramienta que se ve en el panel principal.</summary>
        [ObservableProperty]
        private Tool _current;

        partial void OnSelectedToolChanged(object value)
        {
            if (value is Tool tool)
                Current = tool;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
        private string _selectedProject;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNewVersion))]
        private string _versionNotice;

        [ObservableProperty]
        private Uri _versionUrl;

        public bool HasNewVersion => VersionNotice != null;
        public string ChooseProjectText => L.T("Proyecto abierto en TIA Portal");

        // ---------------------------------------------------------------- Tema e idioma

        public bool SystemTheme => ToolContext.Settings.Theme == Settings.SystemTheme;
        public bool LightTheme => ToolContext.Settings.Theme == Settings.LightTheme;
        public bool DarkTheme => ToolContext.Settings.Theme == Settings.DarkTheme;
        public string ThemeIcon => DarkTheme ? "moon" : LightTheme ? "sun" : "monitor";
        public bool InSpanish => L.Language == L.Castellano;
        public bool InEnglish => L.Language == L.English;
        public string LanguageText => InEnglish ? "EN" : "ES";

        [RelayCommand]
        private void ChangeTheme(string theme)
        {
            ToolContext.Settings.Theme = theme;
            ToolContext.Settings.Save();
            OnPropertyChanged(nameof(SystemTheme));
            OnPropertyChanged(nameof(LightTheme));
            OnPropertyChanged(nameof(DarkTheme));
            OnPropertyChanged(nameof(ThemeIcon));
            ThemeChanged?.Invoke();
        }

        [RelayCommand]
        private void ChangeLanguage(string language)
        {
            if (L.Normalize(language) == L.Language)
                return;
            ToolContext.Settings.Language = language;
            ToolContext.Settings.Save();
            L.Language = L.Normalize(language);
            LanguageChanged?.Invoke();
        }

        [RelayCommand]
        private Task About() => Dialogs.About(L.T("Acerca de"),
            L.T("{0}, versión {1}.\nCreado por Ignacio Suarez (Alomatek).", L.T("Herramientas SICAR"), CurrentVersion.ToString(3)),
            VersionCheck.RepositoryUrl);

        public async Task CheckForUpdates()
        {
            using (var http = new HttpClient())
            {
                var release = await VersionCheck.LatestRelease(http);
                if (release != null && VersionCheck.IsNewer(release, CurrentVersion))
                {
                    VersionUrl = new Uri(release.Url);
                    VersionNotice = L.T("Hay una versión nueva: {0} (tienes la {1}).", release.Tag, CurrentVersion.ToString(3));
                }
            }
        }

        [RelayCommand]
        private void CloseVersionNotice() => VersionNotice = null;

        // ---------------------------------------------------------------- Conexión con TIA

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task FindProjects()
        {
            await ToolContext.Work(L.T("Buscando proyectos abiertos en TIA Portal…"), async () =>
            {
                var open = await (await ToolContext.TiaAsync()).GetOpenProjectsAsync();
                var paths = open.Select(p => p.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Projects.Clear();
                foreach (var path in paths)
                    Projects.Add(path);

                if (!Projects.Contains(SelectedProject))
                    SelectedProject = Projects.FirstOrDefault();

                ToolContext.ShowStatus(Projects.Count == 0
                    ? L.T("No hay ningún proyecto abierto en TIA Portal V19.")
                    : L.T("{0} proyecto(s) abierto(s) en TIA. Elige uno y pulsa Conectar.", Projects.Count));
            });
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private async Task Connect()
        {
            string path = SelectedProject;
            await ToolContext.Work(L.T("Conectando… (si TIA pide permiso para esta aplicación, acéptalo)"), async () =>
            {
                ToolContext.SetTree(new List<DeviceInfo>(), new ProjectTree());
                var devices = (await ToolContext.ConnectAsync(path)).Devices;
                // Primero salen todos sin leer: si la lectura falla, se puede leer cada uno con el botón Leer.
                ToolContext.SetTree(devices, new ProjectTree());
                if (devices.Count == 0)
                {
                    ToolContext.ShowStatus(L.T("Conectado, pero el proyecto no tiene ningún PLC ni HMI."));
                    return;
                }

                var chosen = await ChooseDevices(path, devices);
                if (chosen.Count > 0)
                {
                    ToolContext.ShowStatus(L.T("Leyendo el árbol del proyecto…"));
                    var tree = await (await ToolContext.TiaAsync()).ReadTreeAsync(
                        chosen.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name),
                        chosen.Where(d => d.Kind == DeviceKind.Hmi).Select(d => d.Name), ToolContext.TiaProgress());
                    ToolContext.SetTree(devices, tree);
                }

                int unread = devices.Count - chosen.Count;
                ToolContext.ShowStatus(unread == 0
                    ? L.T("Conectado con {0}.", ToolContext.ProjectName)
                    : L.T("Conectado con {0}. {1} PLC o HMI sin leer: selecciónalo en el árbol y pulsa Leer.", ToolContext.ProjectName, unread));
            });
        }

        /// <summary>
        /// Si el proyecto tiene más de un PLC o HMI, pregunta cuáles leer: leer el árbol de todos tarda minutos en
        /// un proyecto grande. Recuerda la elección por proyecto; la primera vez van todos marcados.
        /// </summary>
        private async Task<List<DeviceInfo>> ChooseDevices(string path, List<DeviceInfo> devices)
        {
            if (devices.Count == 1)
                return devices;

            ToolContext.Settings.ReadDevices.TryGetValue(path, out var previous);
            var checkedIndexes = devices.Select(d => previous == null || previous.Contains(d.Name)).ToList();
            var options = devices.Select(d => DeviceNames.Visible(d, devices) + " · " + (d.Kind == DeviceKind.Hmi ? "HMI" : "PLC")).ToList();

            var chosenIndexes = await Dialogs.ChooseMany(L.T("¿Qué quieres leer del proyecto?"),
                L.T("Leer el árbol de cada PLC o HMI lleva un rato en proyectos grandes. Los que no marques salen en el árbol sin leer; luego puedes leerlos seleccionándolos y pulsando Leer."),
                options, checkedIndexes, L.T("Leer"), L.T("No leer ninguno"));

            var chosen = (chosenIndexes ?? new int[0]).Select(i => devices[i]).ToList();
            ToolContext.Settings.ReadDevices[path] = chosen.Select(d => d.Name).ToList();
            ToolContext.Settings.Save();
            return chosen;
        }

        private bool Idle() => !ToolContext.Busy;
        private bool CanConnect() => !ToolContext.Busy && !string.IsNullOrEmpty(SelectedProject);

        public void Dispose() => ToolContext.Dispose();
    }
}
