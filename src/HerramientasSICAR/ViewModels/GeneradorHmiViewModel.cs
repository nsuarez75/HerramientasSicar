using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica.MenusHmi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Generador HMI: se configuran los menús de las HMI de un PLC (nombres de pantallas, qué pantallas ve cada HMI y
    /// funciones de usuario) y se generan el FC CallHMIMenusConfig_FC y las listas de textos en XML para importar en TIA.
    /// La configuración se guarda en un archivo .hmimenus.
    /// </summary>
    public partial class GeneradorHmiViewModel : Herramienta
    {
        private ProyectoMenus _proyecto;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NombreArchivo))]
        private string _rutaProyecto;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NombreArchivo))]
        private bool _cambiado;

        [ObservableProperty]
        private IdiomaEdicion _idioma;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EsOem), nameof(NotaSubgrupo), nameof(HayNotaSubgrupo), nameof(TieneNombres), nameof(TieneCasillas))]
        private SubgrupoEditable _subgrupoSeleccionado;

        [ObservableProperty]
        private PantallaFunciones _pantallaSeleccionada;

        [ObservableProperty]
        private TextoEditable _tituloOem;

        [ObservableProperty]
        private IReadOnlyList<FilaPantalla> _filas = new List<FilaPantalla>();

        public GeneradorHmiViewModel(Contexto contexto) : base(contexto)
        {
            Subgrupos = CatalogoMenus.Subgrupos
                .Select((s, i) => new SubgrupoEditable(s, i == 0 || CatalogoMenus.Subgrupos[i - 1].Grupo != s.Grupo))
                .ToList();

            string ultimo = Contexto.Ajustes.UltimoProyectoHmi;
            ProyectoMenus proyecto = null;
            if (!string.IsNullOrEmpty(ultimo) && File.Exists(ultimo))
            {
                try
                {
                    proyecto = ProyectoMenus.Abrir(ultimo);
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    proyecto = null;
                }
            }
            Cargar(proyecto ?? ProyectoMenus.Nuevo(), proyecto == null ? null : ultimo);
        }

        public override string Nombre => L.T("Generador HMI");
        public override string Descripcion => L.T("Configura los menús de las HMI del PLC (nombres de pantallas, qué pantallas ve cada HMI y funciones de usuario) y genera el FC CallHMIMenusConfig_FC y las listas de textos en XML para importarlos en TIA.");
        public override string Icono => "monitor";
        public override bool UsaTia => false;

        public IReadOnlyList<SubgrupoEditable> Subgrupos { get; }
        public ObservableCollection<HmiEditable> Hmis { get; } = new ObservableCollection<HmiEditable>();
        public ObservableCollection<PantallaFunciones> PantallasFunciones { get; } = new ObservableCollection<PantallaFunciones>();

        public IReadOnlyList<string> Idiomas { get; } = new[]
        {
            L.T("Texto (para todos los idiomas)"), L.T("Inglés (si es distinto)"), L.T("Alemán (si es distinto)")
        };

        public int IdiomaIndice
        {
            get => (int)Idioma;
            set => Idioma = (IdiomaEdicion)Math.Max(0, value);
        }

        public string NombreArchivo =>
            (RutaProyecto == null ? L.T("Proyecto sin guardar") : Path.GetFileName(RutaProyecto)) + (Cambiado ? " *" : "");

        public bool EsOem => SubgrupoSeleccionado?.Subgrupo.Grupo == CatalogoMenus.Oem;
        public bool TieneNombres => SubgrupoSeleccionado?.Subgrupo.Nombres > 0;
        public bool TieneCasillas => SubgrupoSeleccionado?.Subgrupo.Posiciones > 0;

        public string NotaSubgrupo
        {
            get
            {
                var s = SubgrupoSeleccionado?.Subgrupo;
                if (s == null)
                    return "";
                if (s.Posiciones == 0)
                    return L.T("Solo nombres para la lista {0}: el FC no oculta estas pantallas.", s.Lista.Nombre);
                if (s.Nombres == 0)
                    return L.T("Sin lista de textos: solo se elige qué posiciones se ven en cada HMI.");
                string lista = s.Lista.Nombre + (s.Desplazamiento > 0 ? L.T(" (valores {0} a {1})", s.Desplazamiento + 1, s.Desplazamiento + s.Nombres) : "");
                return L.T("Nombres en la lista {0}. Si una pantalla no tiene nombre, sale el texto estándar.", lista);
            }
        }

        public bool HayNotaSubgrupo => SubgrupoSeleccionado != null;

        // ------------------------------------------------------------------ Proyecto

        private void Cargar(ProyectoMenus proyecto, string ruta)
        {
            _proyecto = proyecto;
            RutaProyecto = ruta;

            Hmis.Clear();
            foreach (var hmi in proyecto.Hmis.OrderBy(h => h.Numero))
                Hmis.Add(new HmiEditable(hmi, this));

            PantallasFunciones.Clear();
            for (int p = 1; p <= CatalogoMenus.PantallasFunciones; p++)
            {
                int pantalla = p;
                var funciones = proyecto.FuncionesUsuario[p - 1].Select((f, i) => new FilaFuncion(i + 1, f, this, pantalla)).ToList();
                PantallasFunciones.Add(new PantallaFunciones(p, proyecto, funciones));
            }

            var anterior = SubgrupoSeleccionado;
            SubgrupoSeleccionado = null;
            SubgrupoSeleccionado = anterior ?? Subgrupos[0];
            PantallaSeleccionada = PantallasFunciones[0];
            Cambiado = false;
        }

        partial void OnSubgrupoSeleccionadoChanged(SubgrupoEditable value) => ReconstruirFilas();

        partial void OnIdiomaChanged(IdiomaEdicion value)
        {
            OnPropertyChanged(nameof(IdiomaIndice));
            foreach (var fila in Filas.Where(f => f.Nombre != null))
                fila.Nombre.Refrescar();
            TituloOem?.Refrescar();
            foreach (var texto in PantallasFunciones.SelectMany(p => p.Funciones).SelectMany(f => f.Textos))
                texto.Refrescar();
        }

        private void ReconstruirFilas()
        {
            var subgrupo = SubgrupoSeleccionado?.Subgrupo;
            if (subgrupo == null)
            {
                Filas = new List<FilaPantalla>();
                TituloOem = null;
                return;
            }

            TituloOem = subgrupo.Grupo == CatalogoMenus.Oem
                ? new TextoEditable(_proyecto.TitulosOem[subgrupo.Id - 1], this, () => $"OEM_{subgrupo.Id:D2}")
                : null;

            var filas = new List<FilaPantalla>();
            for (int posicion = 1; posicion <= subgrupo.Filas; posicion++)
            {
                int p = posicion;
                TextoEditable nombre = null;
                if (posicion <= subgrupo.Nombres)
                    nombre = new TextoEditable(_proyecto.Nombre(subgrupo, posicion), this, () => TextoEstandar(subgrupo, p));
                var casillas = Hmis.Select(h => new CasillaVisible(
                    posicion <= subgrupo.Posiciones ? h.Hmi.Visibles[subgrupo.Clave] : null, posicion - 1, this)).ToList();
                filas.Add(new FilaPantalla(posicion, nombre, casillas));
            }
            Filas = filas;
        }

        private string TextoEstandar(SubgrupoMenu subgrupo, int posicion)
        {
            int idioma = Idioma == IdiomaEdicion.Ingles ? 1 : Idioma == IdiomaEdicion.Aleman ? 2 : 0;
            if (subgrupo.Grupo == CatalogoMenus.Oem)
                return $"<<OEM {subgrupo.Id:D2} screen {posicion:D2}>>";
            return subgrupo.Lista.TextoEstandar(idioma, subgrupo.Desplazamiento + posicion);
        }

        /// <summary>Algo del proyecto ha cambiado (lo llaman las filas al editar).</summary>
        public void MarcarCambiado()
        {
            Cambiado = true;
            foreach (var pantalla in PantallasFunciones)
                pantalla.Refrescar();
            if (PantallaSeleccionada != null)
                foreach (var texto in PantallaSeleccionada.Funciones.SelectMany(f => f.Textos))
                    texto.RefrescarMarca();
        }

        private static TipoArchivo TipoProyecto => new TipoArchivo(L.T("Proyecto del Generador HMI (*.hmimenus)"), "*" + ProyectoMenus.Extension);

        // Si hay cambios sin guardar, pregunta antes de perderlos.
        private async Task<bool> PuedeDescartar() =>
            !Cambiado || await Contexto.Dialogos.Confirmar(L.T("Cambios sin guardar"),
                L.T("El proyecto tiene cambios sin guardar. ¿Descartarlos?"), L.T("Descartar"));

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task Nuevo()
        {
            if (!await PuedeDescartar())
                return;
            Cargar(ProyectoMenus.Nuevo(), null);
            Contexto.MostrarEstado(L.T("Proyecto nuevo con una HMI."));
        }

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task Abrir()
        {
            if (!await PuedeDescartar())
                return;
            string ruta = await Contexto.Dialogos.ElegirArchivo(L.T("Abrir un proyecto del Generador HMI"), CarpetaInicial(), TipoProyecto);
            if (ruta == null)
                return;
            try
            {
                Cargar(ProyectoMenus.Abrir(ruta), ruta);
                RecordarProyecto(ruta);
                Contexto.MostrarEstado(L.T("Proyecto abierto: {0}", ruta));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                Contexto.MostrarEstado(L.T("Error: {0}", e.Message), error: true);
            }
        }

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task Guardar()
        {
            if (RutaProyecto == null)
                await GuardarComo();
            else
                GuardarEn(RutaProyecto);
        }

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task GuardarComo()
        {
            string sugerido = RutaProyecto ?? Path.Combine(CarpetaInicial() ?? "", "Menus HMI" + ProyectoMenus.Extension);
            string ruta = await Contexto.Dialogos.GuardarArchivo(L.T("Guardar el proyecto como…"), sugerido, TipoProyecto);
            if (ruta != null)
                GuardarEn(ruta);
        }

        private void GuardarEn(string ruta)
        {
            try
            {
                _proyecto.Guardar(ruta);
                RutaProyecto = ruta;
                Cambiado = false;
                RecordarProyecto(ruta);
                Contexto.MostrarEstado(L.T("Proyecto guardado: {0}", ruta));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Contexto.MostrarEstado(L.T("Error: {0}", e.Message), error: true);
            }
        }

        private void RecordarProyecto(string ruta)
        {
            Contexto.Ajustes.UltimoProyectoHmi = ruta;
            Contexto.Ajustes.UltimaCarpeta = Path.GetDirectoryName(ruta);
            Contexto.Ajustes.Guardar();
        }

        private string CarpetaInicial() =>
            RutaProyecto != null ? Path.GetDirectoryName(RutaProyecto) : Contexto.Ajustes.UltimaCarpeta;

        // ------------------------------------------------------------------ HMIs

        [RelayCommand(CanExecute = nameof(Libre))]
        private void AnadirHmi()
        {
            if (_proyecto.AnadirHmi() == null)
            {
                Contexto.MostrarEstado(L.T("Ya hay 8 HMI, el máximo (hay 8 IDB HMIMenusConfig)."), error: true);
                return;
            }
            RecargarHmis();
        }

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task QuitarHmi(HmiEditable hmi)
        {
            if (hmi == null || !await Contexto.Dialogos.Confirmar(L.T("Quitar HMI"),
                    L.T("¿Quitar la HMI {0} del proyecto? Se pierde qué pantallas se ven en ella.", hmi.Nombre), L.T("Quitar")))
                return;
            _proyecto.Hmis.Remove(hmi.Hmi);
            RecargarHmis();
        }

        private void RecargarHmis()
        {
            Hmis.Clear();
            foreach (var hmi in _proyecto.Hmis.OrderBy(h => h.Numero))
                Hmis.Add(new HmiEditable(hmi, this));
            ReconstruirFilas();
            MarcarCambiado();
        }

        /// <summary>Marca o desmarca todas las pantallas del subgrupo elegido en una HMI.</summary>
        [RelayCommand]
        private void AlternarColumna(HmiEditable hmi)
        {
            var subgrupo = SubgrupoSeleccionado?.Subgrupo;
            if (hmi == null || subgrupo == null || subgrupo.Posiciones == 0)
                return;
            var visibles = hmi.Hmi.Visibles[subgrupo.Clave];
            bool marcar = visibles.Any(v => !v);
            for (int i = 0; i < visibles.Count; i++)
                visibles[i] = marcar;
            foreach (var fila in Filas)
                foreach (var casilla in fila.Casillas)
                    casilla.Refrescar();
            MarcarCambiado();
        }

        // ------------------------------------------------------------------ Traducir y generar

        private const int MaxTraduccionesConcurrentes = 20;

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task Traducir()
        {
            var textos = TodosLosTextos().Where(t => !string.IsNullOrWhiteSpace(t.Texto)
                && (string.IsNullOrWhiteSpace(t.Ingles) || string.IsNullOrWhiteSpace(t.Aleman))).ToList();
            if (textos.Count == 0)
            {
                Contexto.MostrarEstado(L.T("Todos los textos tienen ya su inglés y su alemán."));
                return;
            }
            if (!await Contexto.Dialogos.Confirmar(L.T("Traducir"),
                    L.T("Se rellenan el inglés y el alemán de {0} textos que no los tienen, con un traductor en línea (necesita internet). Revísalos después eligiendo el idioma arriba.", textos.Count),
                    L.T("Traducir")))
                return;

            await Contexto.Trabajar(L.T("Traduciendo…"), async () =>
            {
                int hechos = 0;
                using (var semaforo = new SemaphoreSlim(MaxTraduccionesConcurrentes))
                {
                    var tareas = textos.Select(async texto =>
                    {
                        await semaforo.WaitAsync();
                        try
                        {
                            string origen = texto.Texto.Trim();
                            string en = string.IsNullOrWhiteSpace(texto.Ingles) ? await ExcelProcessor.TraducirAsync(origen, "en") : null;
                            string de = string.IsNullOrWhiteSpace(texto.Aleman) ? await ExcelProcessor.TraducirAsync(origen, "de") : null;
                            return (texto, en, de);
                        }
                        finally
                        {
                            semaforo.Release();
                            Interlocked.Increment(ref hechos);
                        }
                    }).ToList();
                    var resultados = await Task.WhenAll(tareas);

                    // Una traducción igual al original (nombres de equipos, o fallo del traductor) se deja vacía: sale el texto.
                    foreach (var (texto, en, de) in resultados)
                    {
                        if (en != null && !string.Equals(en.Trim(), texto.Texto.Trim(), StringComparison.OrdinalIgnoreCase))
                            texto.Ingles = en.Trim();
                        if (de != null && !string.Equals(de.Trim(), texto.Texto.Trim(), StringComparison.OrdinalIgnoreCase))
                            texto.Aleman = de.Trim();
                    }
                }
                OnIdiomaChanged(Idioma);
                MarcarCambiado();
                Contexto.MostrarEstado(L.T("Traducidos {0} textos. Revísalos eligiendo Inglés o Alemán arriba.", hechos));
            });
        }

        private IEnumerable<TextoHmi> TodosLosTextos() =>
            _proyecto.TitulosOem
                .Concat(_proyecto.Nombres.Values.SelectMany(n => n))
                .Concat(_proyecto.FuncionesUsuario.SelectMany(p => p).SelectMany(f => f.Textos()));

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task Generar()
        {
            var errores = _proyecto.Errores();
            if (errores.Count > 0)
            {
                await Contexto.Dialogos.Mensaje(L.T("No se puede generar"), string.Join("\n", errores));
                return;
            }
            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta donde generar el FC y las listas de textos"),
                Contexto.Ajustes.UltimaCarpetaHmiGenerada ?? CarpetaInicial());
            if (carpeta == null)
                return;
            Contexto.Ajustes.UltimaCarpetaHmiGenerada = carpeta;
            Contexto.Ajustes.Guardar();

            var proyecto = _proyecto;
            await Contexto.Trabajar(L.T("Generando…"), async () =>
            {
                var escritos = await Task.Run(() => GeneradorMenus.Generar(proyecto, carpeta));
                Contexto.MostrarEstado(L.T("Generados el FC y {0} listas de textos en {1}", escritos.Count - 1, carpeta));
            });
        }

        protected override void ActualizarComandos()
        {
            NuevoCommand.NotifyCanExecuteChanged();
            AbrirCommand.NotifyCanExecuteChanged();
            GuardarCommand.NotifyCanExecuteChanged();
            GuardarComoCommand.NotifyCanExecuteChanged();
            AnadirHmiCommand.NotifyCanExecuteChanged();
            QuitarHmiCommand.NotifyCanExecuteChanged();
            TraducirCommand.NotifyCanExecuteChanged();
            GenerarCommand.NotifyCanExecuteChanged();
        }
    }
}
