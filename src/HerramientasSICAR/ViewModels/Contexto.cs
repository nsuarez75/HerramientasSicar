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
    public partial class Contexto : ObservableObject, IDisposable
    {
        // El puente de TIA-Connector se arranca al primer uso: si no hay TIA instalado, se avisa en vez de no arrancar.
        private TiaClient _tia;

        public Contexto(Ajustes ajustes)
        {
            Ajustes = ajustes;
            _estado = L.T("Abre el proyecto en TIA Portal, elígelo arriba y pulsa Conectar.");
        }

        public Ajustes Ajustes { get; }

        public IDialogos Dialogos { get; set; }

        public ObservableCollection<string> Fallos { get; } = new ObservableCollection<string>();

        /// <summary>Ruta del proyecto conectado, o null.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Conectado), nameof(SinConectar), nameof(NombreProyecto))]
        private string _proyectoConectado;

        [ObservableProperty]
        private string _estado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstadoNormal))]
        private bool _estadoError;

        [ObservableProperty]
        private bool _ocupado;

        public bool Conectado => ProyectoConectado != null;
        public bool SinConectar => !Conectado;
        public bool EstadoNormal => !EstadoError;
        public bool HayFallos => Fallos.Count > 0;
        public string NombreProyecto => Conectado ? Path.GetFileNameWithoutExtension(ProyectoConectado) : null;

        /// <summary>Ocupado cambió: las herramientas actualizan sus botones.</summary>
        public event Action OcupadoCambiado;

        partial void OnOcupadoChanged(bool value) => OcupadoCambiado?.Invoke();

        /// <summary>
        /// El cliente de TIA-Connector. La primera vez arranca el puente (un proceso aparte que es el único que habla con
        /// TIA); si el puente se ha cerrado, arranca otro. Todas las herramientas lo usan para todo lo que hacen con TIA.
        /// </summary>
        public async Task<TiaClient> TiaAsync()
        {
            if (!Program.HayTia)
                throw new InvalidOperationException(L.T("No se encuentra TIA Portal V19 con Openness en este equipo."));
            return _tia ?? (_tia = await TiaClient.StartAsync());
        }

        /// <summary>Proyecto conectado (nombre, carpeta, idioma de edición, PLC y HMI), o null.</summary>
        public ProjectInfo Proyecto { get; private set; }

        /// <summary>Se engancha al TIA que tiene abierto el proyecto. La primera vez TIA pide permiso al usuario.</summary>
        public async Task<ProjectInfo> ConectarAsync(string ruta)
        {
            Proyecto = null;
            ProyectoConectado = null;
            var proyecto = await (await TiaAsync()).ConnectAsync(ruta);
            Proyecto = proyecto;
            ProyectoConectado = ruta;
            return proyecto;
        }

        /// <summary>Progreso de TIA-Connector como texto en la barra de estado.</summary>
        public IProgress<ProgressInfo> ProgresoTia() => new Progress<ProgressInfo>(p => MostrarEstado(TextosTia.Progreso(p)));

        /// <summary>
        /// Pone abajo los fallos del resultado (más los <paramref name="previos"/>) y en la barra de estado el mensaje,
        /// con cuántos fallaron. Devuelve los fallos.
        /// </summary>
        public List<string> MostrarResultado(BatchResult resultado, string mensaje, bool importando = false, IEnumerable<string> previos = null)
        {
            var fallos = (previos ?? Enumerable.Empty<string>()).Concat(TextosTia.Fallos(resultado, importando)).ToList();
            PonerFallos(fallos);
            MostrarEstado(fallos.Count == 0 ? mensaje : mensaje + " " + L.T("{0} con error (ver abajo).", fallos.Count), error: fallos.Count > 0);
            return fallos;
        }

        public void MostrarEstado(string texto, bool error = false)
        {
            Estado = texto;
            EstadoError = error;
        }

        public void PonerFallos(IEnumerable<string> fallos)
        {
            Fallos.Clear();
            foreach (var fallo in fallos)
                Fallos.Add(fallo);
            OnPropertyChanged(nameof(HayFallos));
        }

        /// <summary>
        /// Ejecuta un trabajo largo: marca la app como ocupada, muestra el mensaje abajo y,
        /// si algo falla, pone el error en la barra de estado.
        /// </summary>
        public async Task Trabajar(string mensaje, Func<Task> trabajo)
        {
            Ocupado = true;
            MostrarEstado(mensaje);
            PonerFallos(Enumerable.Empty<string>());
            try
            {
                await trabajo();
            }
            catch (TiaConnectorException ex)
            {
                AlFallarTia(ex);
            }
            catch (Exception ex)
            {
                MostrarEstado(L.T("Error: {0}", ex.Message), error: true);
            }
            finally
            {
                Ocupado = false;
            }
        }

        // ------------------------------------------------------------------ Árbol del proyecto, común a las herramientas

        private readonly Dictionary<string, PlcTree> _plcsLeidos = new Dictionary<string, PlcTree>();

        /// <summary>PLC y HMI del proyecto conectado (vacío sin conexión).</summary>
        public IReadOnlyList<DeviceInfo> Dispositivos { get; private set; } = new List<DeviceInfo>();

        /// <summary>Árbol leído de un PLC, o null si no se ha leído (se eligió no leerlo al conectar).</summary>
        public PlcTree PlcLeido(string nombre) => _plcsLeidos.TryGetValue(nombre, out var plc) ? plc : null;

        /// <summary>
        /// El árbol cambió: todo (al conectar o desconectar; trae el árbol completo, con las HMI) o el de un PLC
        /// (al leerlo de nuevo). Lo escuchan Importar / Exportar y los selectores de bloques de las herramientas.
        /// </summary>
        public event Action<CambioArbol> ArbolCambiado;

        public void PonerArbol(IReadOnlyList<DeviceInfo> dispositivos, ProjectTree arbol)
        {
            Dispositivos = dispositivos;
            _plcsLeidos.Clear();
            foreach (var plc in arbol.Plcs)
                _plcsLeidos[plc.Name] = plc;
            ArbolCambiado?.Invoke(new CambioArbol(dispositivos, arbol, null));
        }

        /// <summary>Vuelve a leer de TIA el árbol de un PLC y avisa a todas las herramientas.</summary>
        public async Task<PlcTree> LeerPlcAsync(string nombre)
        {
            var plc = await (await TiaAsync()).ReadPlcAsync(nombre);
            _plcsLeidos[plc.Name] = plc;
            ArbolCambiado?.Invoke(new CambioArbol(Dispositivos, null, plc));
            return plc;
        }

        /// <summary>
        /// Error de una petición entera a TIA: lo dice en castellano y, si se ha perdido la conexión (TIA o el puente
        /// se han cerrado), deja la app desconectada.
        /// </summary>
        private void AlFallarTia(TiaConnectorException ex)
        {
            MostrarEstado(TextosTia.Error(ex), error: true);
            if (ex.Code == ErrorCode.BridgeExited)
            {
                // El puente ya no está: el próximo uso arranca otro.
                _tia?.Dispose();
                _tia = null;
            }
            if (ex.Code == ErrorCode.BridgeExited || ex.Code == ErrorCode.TiaClosed || ex.Code == ErrorCode.ProjectNotOpen
                || ex.Code == ErrorCode.NotConnected)
            {
                Proyecto = null;
                ProyectoConectado = null;
            }
        }

        /// <summary>
        /// Cierra el puente: termina lo pendiente y sale. El TIA del usuario sigue abierto (el puente nunca lo cierra).
        /// </summary>
        public void Dispose() => _tia?.Dispose();
    }

    /// <summary>Qué ha cambiado del árbol del proyecto: todo (<see cref="Arbol"/>) o un PLC (<see cref="Plc"/>).</summary>
    public sealed class CambioArbol
    {
        public CambioArbol(IReadOnlyList<DeviceInfo> dispositivos, ProjectTree arbol, PlcTree plc)
        {
            Dispositivos = dispositivos;
            Arbol = arbol;
            Plc = plc;
        }

        public IReadOnlyList<DeviceInfo> Dispositivos { get; }

        /// <summary>Árbol completo (con las HMI) si ha cambiado todo; null si solo ha cambiado un PLC.</summary>
        public ProjectTree Arbol { get; }

        public PlcTree Plc { get; }
        public bool Completo => Arbol != null;
    }
}
