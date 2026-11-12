using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
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
        private readonly ImportarExportarViewModel _importarExportar;

        public MainViewModel(Ajustes ajustes)
        {
            Contexto = new Contexto(ajustes);
            Contexto.OcupadoCambiado += () =>
            {
                BuscarProyectosCommand.NotifyCanExecuteChanged();
                ConectarCommand.NotifyCanExecuteChanged();
            };

            _importarExportar = new ImportarExportarViewModel(Contexto);
            // Todas trabajan con el proyecto conectado; el Generador HMI también edita y genera sin conexión.
            foreach (var herramienta in new Herramienta[]
            {
                _importarExportar,
                new GeneradorHmiViewModel(Contexto),
                new ComentadorViewModel(Contexto),
                new SincroCheckerViewModel(Contexto),
                new DiagExpectedViewModel(Contexto),
                new NumeradorViewModel(Contexto),
                new RenombrarArrayViewModel(Contexto),
            })
                Lateral.Add(herramienta);

            _seleccionada = _actual = _importarExportar;
        }

        public Contexto Contexto { get; }

        public IDialogos Dialogos
        {
            get => Contexto.Dialogos;
            set => Contexto.Dialogos = value;
        }

        /// <summary>El tema cambió: la aplicación lo aplica.</summary>
        public event Action TemaCambiado;

        /// <summary>El idioma cambió: los textos se traducen al cargar las vistas, así que se monta otra ventana.</summary>
        public event Action IdiomaCambiado;

        public static Version VersionActual => Assembly.GetExecutingAssembly().GetName().Version;
        public string Titulo => L.T("Herramientas SICAR") + " " + VersionActual.ToString(3);
        public string NombreApp => L.T("Herramientas SICAR");
        public string Subtitulo => L.T("Utilidades para proyectos SICAR@GST en TIA Portal");

        /// <summary>Rótulos de grupo y herramientas, en el orden en que se ven en el lateral.</summary>
        public ObservableCollection<Herramienta> Lateral { get; } = new ObservableCollection<Herramienta>();

        public ObservableCollection<string> Proyectos { get; } = new ObservableCollection<string>();

        /// <summary>Lo seleccionado en el lateral. Los rótulos de grupo están desactivados, pero si llegan aquí no cambian la herramienta.</summary>
        [ObservableProperty]
        private object _seleccionada;

        /// <summary>Herramienta que se ve en el panel principal.</summary>
        [ObservableProperty]
        private Herramienta _actual;

        partial void OnSeleccionadaChanged(object value)
        {
            if (value is Herramienta herramienta)
                Actual = herramienta;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConectarCommand))]
        private string _proyectoSeleccionado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayVersionNueva))]
        private string _avisoVersion;

        [ObservableProperty]
        private Uri _urlVersion;

        public bool HayVersionNueva => AvisoVersion != null;
        public string TextoElegirProyecto => L.T("Proyecto abierto en TIA Portal");

        // ---------------------------------------------------------------- Tema e idioma

        public bool TemaSistema => Contexto.Ajustes.Tema == Ajustes.TemaSistema;
        public bool TemaClaro => Contexto.Ajustes.Tema == Ajustes.TemaClaro;
        public bool TemaOscuro => Contexto.Ajustes.Tema == Ajustes.TemaOscuro;
        public string IconoTema => TemaOscuro ? "moon" : TemaClaro ? "sun" : "monitor";
        public bool EnCastellano => L.Idioma == L.Castellano;
        public bool EnIngles => L.Idioma == L.Ingles;
        public string TextoIdioma => EnIngles ? "EN" : "ES";

        [RelayCommand]
        private void CambiarTema(string tema)
        {
            Contexto.Ajustes.Tema = tema;
            Contexto.Ajustes.Guardar();
            OnPropertyChanged(nameof(TemaSistema));
            OnPropertyChanged(nameof(TemaClaro));
            OnPropertyChanged(nameof(TemaOscuro));
            OnPropertyChanged(nameof(IconoTema));
            TemaCambiado?.Invoke();
        }

        [RelayCommand]
        private void CambiarIdioma(string idioma)
        {
            if (L.Normalizar(idioma) == L.Idioma)
                return;
            Contexto.Ajustes.Idioma = idioma;
            Contexto.Ajustes.Guardar();
            L.Idioma = L.Normalizar(idioma);
            IdiomaCambiado?.Invoke();
        }

        [RelayCommand]
        private Task AcercaDe() => Dialogos.AcercaDe(L.T("Acerca de"),
            L.T("{0}, versión {1}.\nCreado por Ignacio Suarez (Alomatek).", L.T("Herramientas SICAR"), VersionActual.ToString(3)),
            ComprobarVersion.UrlRepositorio);

        public async Task ComprobarActualizaciones()
        {
            using (var http = new HttpClient())
            {
                var release = await ComprobarVersion.UltimaRelease(http);
                if (release != null && ComprobarVersion.EsMasNueva(release, VersionActual))
                {
                    UrlVersion = new Uri(release.Url);
                    AvisoVersion = L.T("Hay una versión nueva: {0} (tienes la {1}).", release.Tag, VersionActual.ToString(3));
                }
            }
        }

        [RelayCommand]
        private void CerrarAvisoVersion() => AvisoVersion = null;

        // ---------------------------------------------------------------- Conexión con TIA

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task BuscarProyectos()
        {
            await Contexto.Trabajar(L.T("Buscando proyectos abiertos en TIA Portal…"), async () =>
            {
                var abiertos = await (await Contexto.TiaAsync()).GetOpenProjectsAsync();
                var rutas = abiertos.Select(p => p.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Proyectos.Clear();
                foreach (var ruta in rutas)
                    Proyectos.Add(ruta);

                if (!Proyectos.Contains(ProyectoSeleccionado))
                    ProyectoSeleccionado = Proyectos.FirstOrDefault();

                Contexto.MostrarEstado(Proyectos.Count == 0
                    ? L.T("No hay ningún proyecto abierto en TIA Portal V19.")
                    : L.T("{0} proyecto(s) abierto(s) en TIA. Elige uno y pulsa Conectar.", Proyectos.Count));
            });
        }

        [RelayCommand(CanExecute = nameof(PuedeConectar))]
        private async Task Conectar()
        {
            string ruta = ProyectoSeleccionado;
            await Contexto.Trabajar(L.T("Conectando… (si TIA pide permiso para esta aplicación, acéptalo)"), async () =>
            {
                Contexto.PonerArbol(new List<DeviceInfo>(), new ProjectTree());
                var dispositivos = (await Contexto.ConectarAsync(ruta)).Devices;
                // Primero salen todos sin leer: si la lectura falla, se puede leer cada uno con el botón Leer.
                Contexto.PonerArbol(dispositivos, new ProjectTree());
                if (dispositivos.Count == 0)
                {
                    Contexto.MostrarEstado(L.T("Conectado, pero el proyecto no tiene ningún PLC ni HMI."));
                    return;
                }

                var elegidos = await ElegirDispositivos(ruta, dispositivos);
                if (elegidos.Count > 0)
                {
                    Contexto.MostrarEstado(L.T("Leyendo el árbol del proyecto…"));
                    var arbol = await (await Contexto.TiaAsync()).ReadTreeAsync(
                        elegidos.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name),
                        elegidos.Where(d => d.Kind == DeviceKind.Hmi).Select(d => d.Name), Contexto.ProgresoTia());
                    Contexto.PonerArbol(dispositivos, arbol);
                }

                int sinLeer = dispositivos.Count - elegidos.Count;
                Contexto.MostrarEstado(sinLeer == 0
                    ? L.T("Conectado con {0}.", Contexto.NombreProyecto)
                    : L.T("Conectado con {0}. {1} PLC o HMI sin leer: selecciónalo en el árbol y pulsa Leer.", Contexto.NombreProyecto, sinLeer));
            });
        }

        /// <summary>
        /// Si el proyecto tiene más de un PLC o HMI, pregunta cuáles leer: leer el árbol de todos tarda minutos en
        /// un proyecto grande. Recuerda la elección por proyecto; la primera vez van todos marcados.
        /// </summary>
        private async Task<List<DeviceInfo>> ElegirDispositivos(string ruta, List<DeviceInfo> dispositivos)
        {
            if (dispositivos.Count == 1)
                return dispositivos;

            Contexto.Ajustes.DispositivosLeidos.TryGetValue(ruta, out var anteriores);
            var marcadas = dispositivos.Select(d => anteriores == null || anteriores.Contains(d.Name)).ToList();
            var opciones = dispositivos.Select(d => d.Name + " · " + (d.Kind == DeviceKind.Hmi ? "HMI" : "PLC")).ToList();

            var elegidas = await Dialogos.ElegirVarios(L.T("¿Qué quieres leer del proyecto?"),
                L.T("Leer el árbol de cada PLC o HMI lleva un rato en proyectos grandes. Los que no marques salen en el árbol sin leer; luego puedes leerlos seleccionándolos y pulsando Leer."),
                opciones, marcadas, L.T("Leer"), L.T("No leer ninguno"));

            var elegidos = (elegidas ?? new int[0]).Select(i => dispositivos[i]).ToList();
            Contexto.Ajustes.DispositivosLeidos[ruta] = elegidos.Select(d => d.Name).ToList();
            Contexto.Ajustes.Guardar();
            return elegidos;
        }

        private bool Libre() => !Contexto.Ocupado;
        private bool PuedeConectar() => !Contexto.Ocupado && !string.IsNullOrEmpty(ProyectoSeleccionado);

        public void Dispose() => Contexto.Dispose();
    }
}
