using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Client;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Lo que comparten la ventana y las herramientas: la conexión con TIA, los diálogos
    /// y la barra de estado de abajo.
    /// </summary>
    public partial class ToolContext : ObservableObject, IDisposable
    {
        // El puente de TIA-Connector se arranca al primer uso: si no hay TIA instalado, se avisa en vez de no arrancar.
        private TiaClient _tia;

        public ToolContext(Settings settings)
        {
            Settings = settings;
            _status = L.T("Abre el proyecto en TIA Portal, elígelo arriba y pulsa Conectar.");
        }

        public Settings Settings { get; }

        public IDialogs Dialogs { get; set; }

        public ObservableCollection<string> Failures { get; } = new ObservableCollection<string>();

        /// <summary>Ruta del proyecto conectado, o null.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Connected), nameof(NotConnected), nameof(ProjectName))]
        private string _connectedProject;

        [ObservableProperty]
        private string _status;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusNormal))]
        private bool _statusError;

        [ObservableProperty]
        private bool _busy;

        public bool Connected => ConnectedProject != null;
        public bool NotConnected => !Connected;
        public bool StatusNormal => !StatusError;
        public bool HasFailures => Failures.Count > 0;
        public string ProjectName => Connected ? Path.GetFileNameWithoutExtension(ConnectedProject) : null;

        /// <summary>Ocupado cambió: las herramientas actualizan sus botones.</summary>
        public event Action BusyChanged;

        partial void OnBusyChanged(bool value) => BusyChanged?.Invoke();

        /// <summary>
        /// El cliente de TIA-Connector. La primera vez arranca el puente (un proceso aparte que es el único que habla con
        /// TIA); si el puente se ha cerrado, arranca otro. Todas las herramientas lo usan para todo lo que hacen con TIA.
        /// </summary>
        public async Task<TiaClient> TiaAsync()
        {
            if (!Program.HasTia)
                throw new InvalidOperationException(L.T("No se encuentra TIA Portal V19 con Openness en este equipo."));
            return _tia ?? (_tia = await TiaClient.StartAsync());
        }

        /// <summary>Proyecto conectado (nombre, carpeta, idioma de edición, PLC y HMI), o null.</summary>
        public ProjectInfo ProjectFile { get; private set; }

        /// <summary>Se engancha al TIA que tiene abierto el proyecto. La primera vez TIA pide permiso al usuario.</summary>
        public async Task<ProjectInfo> ConnectAsync(string path)
        {
            ProjectFile = null;
            ConnectedProject = null;
            var project = await (await TiaAsync()).ConnectAsync(path);
            ProjectFile = project;
            ConnectedProject = path;
            return project;
        }

        /// <summary>Progreso de TIA-Connector como texto en la barra de estado.</summary>
        public IProgress<ProgressInfo> TiaProgress() => new Progress<ProgressInfo>(p => ShowStatus(TiaTexts.Progress(p)));

        /// <summary>
        /// Pone abajo los fallos del resultado (más los <paramref name="previos"/>) y en la barra de estado el mensaje,
        /// con cuántos fallaron. Devuelve los fallos.
        /// </summary>
        public List<string> ShowResult(BatchResult result, string message, bool importing = false, IEnumerable<string> previous = null)
        {
            var failures = (previous ?? Enumerable.Empty<string>()).Concat(TiaTexts.Failures(result, importing)).ToList();
            SetFailures(failures);
            ShowStatus(failures.Count == 0 ? message : message + " " + L.T("{0} con error (ver abajo).", failures.Count), error: failures.Count > 0);
            return failures;
        }

        public void ShowStatus(string text, bool error = false)
        {
            Status = text;
            StatusError = error;
        }

        public void SetFailures(IEnumerable<string> failures)
        {
            Failures.Clear();
            foreach (var failure in failures)
                Failures.Add(failure);
            OnPropertyChanged(nameof(HasFailures));
        }

        /// <summary>
        /// Ejecuta un trabajo largo: marca la app como ocupada, muestra el mensaje abajo y,
        /// si algo falla, pone el error en la barra de estado.
        /// </summary>
        public async Task Work(string message, Func<Task> work)
        {
            Busy = true;
            ShowStatus(message);
            SetFailures(Enumerable.Empty<string>());
            try
            {
                await work();
            }
            catch (TiaConnectorException ex)
            {
                OnTiaFailed(ex);
            }
            catch (Exception ex)
            {
                ShowStatus(L.T("Error: {0}", ex.Message), error: true);
            }
            finally
            {
                Busy = false;
            }
        }

        // ------------------------------------------------------------------ Árbol del proyecto, común a las herramientas

        private readonly Dictionary<string, PlcTree> _readPlcs = new Dictionary<string, PlcTree>();

        /// <summary>PLC y HMI del proyecto conectado (vacío sin conexión).</summary>
        public IReadOnlyList<DeviceInfo> Devices { get; private set; } = new List<DeviceInfo>();

        /// <summary>Nombre con el que se enseña un PLC o una HMI del proyecto (en un IPC, el de la estación).</summary>
        public string DisplayName(string device)
        {
            var info = Devices.FirstOrDefault(d => d.Name == device);
            return info == null ? device : Logic.DeviceNames.Visible(info, Devices);
        }

        /// <summary>Árbol leído de un PLC, o null si no se ha leído (se eligió no leerlo al conectar).</summary>
        public PlcTree ReadPlc(string name) => _readPlcs.TryGetValue(name, out var plc) ? plc : null;

        /// <summary>
        /// El árbol cambió: todo (al conectar o desconectar; trae el árbol completo, con las HMI) o el de un PLC
        /// (al leerlo de nuevo). Lo escuchan Importar / Exportar y los selectores de bloques de las herramientas.
        /// </summary>
        public event Action<TreeChange> TreeChanged;

        public void SetTree(IReadOnlyList<DeviceInfo> devices, ProjectTree tree)
        {
            Devices = devices;
            _readPlcs.Clear();
            foreach (var plc in tree.Plcs)
                _readPlcs[plc.Name] = plc;
            TreeChanged?.Invoke(new TreeChange(devices, tree, null));
        }

        /// <summary>Vuelve a leer de TIA el árbol de un PLC y avisa a todas las herramientas.</summary>
        public async Task<PlcTree> ReadPlcAsync(string name)
        {
            var plc = await (await TiaAsync()).ReadPlcAsync(name);
            _readPlcs[plc.Name] = plc;
            TreeChanged?.Invoke(new TreeChange(Devices, null, plc));
            return plc;
        }

        /// <summary>
        /// Error de una petición entera a TIA: lo dice en castellano y, si se ha perdido la conexión (TIA o el puente
        /// se han cerrado), deja la app desconectada.
        /// </summary>
        private void OnTiaFailed(TiaConnectorException ex)
        {
            ShowStatus(TiaTexts.Error(ex), error: true);
            if (ex.Code == ErrorCode.BridgeExited)
            {
                // El puente ya no está: el próximo uso arranca otro.
                _tia?.Dispose();
                _tia = null;
            }
            if (ex.Code == ErrorCode.BridgeExited || ex.Code == ErrorCode.TiaClosed || ex.Code == ErrorCode.ProjectNotOpen
                || ex.Code == ErrorCode.NotConnected)
            {
                ProjectFile = null;
                ConnectedProject = null;
            }
        }

        /// <summary>
        /// Cierra el puente: termina lo pendiente y sale. El TIA del usuario sigue abierto (el puente nunca lo cierra).
        /// </summary>
        public void Dispose() => _tia?.Dispose();
    }

    /// <summary>Qué ha cambiado del árbol del proyecto: todo (<see cref="Arbol"/>) o un PLC (<see cref="Plc"/>).</summary>
    public sealed class TreeChange
    {
        public TreeChange(IReadOnlyList<DeviceInfo> devices, ProjectTree tree, PlcTree plc)
        {
            Devices = devices;
            Tree = tree;
            Plc = plc;
        }

        public IReadOnlyList<DeviceInfo> Devices { get; }

        /// <summary>Árbol completo (con las HMI) si ha cambiado todo; null si solo ha cambiado un PLC.</summary>
        public ProjectTree Tree { get; }

        public PlcTree Plc { get; }
        public bool IsFull => Tree != null;
    }
}
