using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Rótulo de un grupo en la lista lateral («Con TIA», «Con archivos»): no se puede seleccionar.</summary>
    public sealed class GrupoLateral
    {
        private readonly string _titulo;

        public GrupoLateral(string titulo)
        {
            _titulo = titulo;
        }

        // Se traduce al pintarlo: al cambiar de idioma la ventana se vuelve a crear.
        public string Titulo => L.T(_titulo);
        public bool EsSeleccionable => false;
    }

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
            var conTia = new Herramienta[]
            {
                _importarExportar,
                new DiagExpectedViewModel(Contexto),
                new NumeradorViewModel(Contexto),
                new RenombrarArrayViewModel(Contexto),
                new SincroCheckerViewModel(Contexto),
            };
            var conArchivos = new Herramienta[]
            {
                new ComentadorViewModel(Contexto),
                new GeneradorHmiViewModel(Contexto),
            };

            Lateral.Add(new GrupoLateral("CON TIA"));
            foreach (var h in conTia)
                Lateral.Add(h);
            Lateral.Add(new GrupoLateral("CON ARCHIVOS"));
            foreach (var h in conArchivos)
                Lateral.Add(h);

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
        public ObservableCollection<object> Lateral { get; } = new ObservableCollection<object>();

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
                var rutas = await Contexto.Tia().ListarProyectosAbiertosAsync();
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
                Contexto.ProyectoConectado = null;
                _importarExportar.MostrarArbol(new Services.ArbolProyecto());
                var arbol = await Contexto.Tia().ConectarAsync(ruta);
                _importarExportar.MostrarArbol(arbol);
                Contexto.ProyectoConectado = ruta;
                Contexto.MostrarEstado(arbol.Plcs.Count == 0 && arbol.Hmis.Count == 0
                    ? L.T("Conectado, pero el proyecto no tiene ningún PLC ni HMI.")
                    : L.T("Conectado con {0}.", Contexto.NombreProyecto));
            });
        }

        private bool Libre() => !Contexto.Ocupado;
        private bool PuedeConectar() => !Contexto.Ocupado && !string.IsNullOrEmpty(ProyectoSeleccionado);

        public void Dispose() => Contexto.Dispose();
    }
}
