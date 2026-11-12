using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Logica.MenusHmi;
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

        /// <summary>Nodo elegido en el árbol (grupo o subgrupo).</summary>
        [ObservableProperty]
        private NodoMenu _nodoSeleccionado;

        /// <summary>Subgrupo cuya tabla se ve: el último subgrupo elegido en el árbol.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EsOem), nameof(NotaSubgrupo), nameof(HayNotaSubgrupo), nameof(TieneNombres), nameof(TieneCasillas))]
        private SubgrupoMenu _subgrupoActual;

        [ObservableProperty]
        private PantallaFunciones _pantallaSeleccionada;

        [ObservableProperty]
        private DatasetProduccion _datasetSeleccionado;

        [ObservableProperty]
        private TextoEditable _tituloOem;

        [ObservableProperty]
        private IReadOnlyList<FilaPantalla> _filas = new List<FilaPantalla>();

        public GeneradorHmiViewModel(Contexto contexto) : base(contexto)
        {
            Grupos = CatalogoMenus.Subgrupos.GroupBy(s => s.Grupo).Select(g => NodoMenu.DeGrupo(g.Key, g)).ToList();

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

            GruposImportar = new[]
            {
                new GrupoImportar(L.T("Bloque"), new[] { GeneradorMenus.NombreFc }),
                new GrupoImportar(L.T("Listas de menús y funciones de usuario"),
                    CatalogoMenus.Listas.Select(l => l.Nombre).Concat(new[] { CatalogoMenus.ListaTextosFunciones })),
                new GrupoImportar(L.T("Listas de Production data"),
                    CatalogoMenus.DatosProduccion.SelectMany(t => Enumerable.Range(1, CatalogoMenus.DatasetsProduccion).Select(t.NombreLista))),
            };
            foreach (var componente in GruposImportar.SelectMany(g => g.Componentes))
                componente.PropertyChanged += (_, __) => ImportarEnTiaCommand.NotifyCanExecuteChanged();
            Contexto.ArbolCambiado += cambio => { if (cambio.Completo) PonerDestinos(); };
            PonerDestinos();
        }

        public override string Nombre => L.T("Generador HMI");
        public override string Descripcion => L.T("Configura los menús de las HMI del PLC (nombres de pantallas, qué pantallas ve cada HMI y funciones de usuario) y genera el FC CallHMIMenusConfig_FC y las listas de textos en XML para importarlos en TIA.");
        public override string Icono => "monitor";
        public override bool UsaTia => false;

        public IReadOnlyList<NodoMenu> Grupos { get; }

        // ------------------------------------------------------------------ Importar en TIA

        /// <summary>Pestaña que se ve (0 Pantallas, 1 Funciones de usuario, 2 Production data, 3 Importar en TIA).</summary>
        [ObservableProperty]
        private int _pestana;

        public IReadOnlyList<GrupoImportar> GruposImportar { get; }

        /// <summary>PLC del proyecto conectado, para el FC.</summary>
        public ObservableCollection<string> Plcs { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ImportarEnTiaCommand))]
        private string _plcDestino;

        /// <summary>HMI del proyecto conectado, para las listas de textos.</summary>
        public ObservableCollection<DestinoHmi> HmisDestino { get; } = new ObservableCollection<DestinoHmi>();

        public bool HayDispositivos => Plcs.Count > 0 || HmisDestino.Count > 0;

        // PLC y HMI del proyecto conectado (se rehacen al conectar). Se conserva lo elegido si sigue existiendo.
        private void PonerDestinos()
        {
            string plc = PlcDestino;
            var hmisMarcadas = HmisDestino.Where(h => h.Marcado).Select(h => h.Nombre).ToList();
            bool primeraVez = HmisDestino.Count == 0;

            Plcs.Clear();
            HmisDestino.Clear();
            foreach (var dispositivo in Contexto.Dispositivos)
            {
                if (dispositivo.Kind == DeviceKind.Hmi)
                {
                    var destino = new DestinoHmi(dispositivo.Name, Contexto.NombreVisible(dispositivo.Name),
                        primeraVez || hmisMarcadas.Contains(dispositivo.Name));
                    destino.PropertyChanged += (_, __) => ImportarEnTiaCommand.NotifyCanExecuteChanged();
                    HmisDestino.Add(destino);
                }
                else
                    Plcs.Add(dispositivo.Name);
            }
            PlcDestino = Plcs.Contains(plc) ? plc : Plcs.FirstOrDefault();
            OnPropertyChanged(nameof(HayDispositivos));
            ImportarEnTiaCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void IrAImportar() => Pestana = 3;

        /// <summary>
        /// Genera en una carpeta temporal e importa lo marcado: el FC en el PLC (en su carpeta si ya está, si no en
        /// 08_HMI_Management › 01_HMI_Configuration) y las listas en cada HMI marcada. Borra la carpeta al terminar.
        /// </summary>
        [RelayCommand(CanExecute = nameof(PuedeImportarEnTia))]
        private async Task ImportarEnTia()
        {
            var errores = _proyecto.Errores();
            if (errores.Count > 0)
            {
                await Contexto.Dialogos.Mensaje(L.T("No se puede generar"), string.Join("\n", errores));
                return;
            }

            bool fc = GruposImportar[0].Componentes[0].Marcado;
            var listas = GruposImportar.Skip(1).SelectMany(g => g.Componentes).Where(c => c.Marcado).Select(c => c.Nombre).ToList();
            var hmis = HmisDestino.Where(h => h.Marcado).Select(h => h.Nombre).ToList();
            if (listas.Count == 0)
                hmis.Clear();
            string plc = fc ? PlcDestino : null;

            var partes = new List<string>();
            if (plc != null)
                partes.Add(L.T("el FC {0} en {1}", GeneradorMenus.NombreFc, plc));
            if (hmis.Count > 0)
                partes.Add(L.T("{0} lista(s) de textos en {1}", listas.Count, string.Join(", ", hmis.Select(Contexto.NombreVisible))));
            if (!await Contexto.Dialogos.Confirmar(L.T("Importar en TIA"),
                    L.T("Se importan {0}. Lo que ya exista con el mismo nombre se sobrescribe. Guarda antes el proyecto en TIA.", string.Join(L.T(" y "), partes)),
                    L.T("Importar")))
                return;

            var proyecto = _proyecto;
            await Contexto.Trabajar(L.T("Generando…"), async () =>
            {
                var tia = await Contexto.TiaAsync();
                var progreso = Contexto.ProgresoTia();
                int importados = 0;
                var fallos = new List<string>();
                using (var temporal = new TempFolder())
                {
                    await Task.Run(() => GeneradorMenus.Generar(proyecto, temporal.Path));

                    if (plc != null)
                    {
                        // En su carpeta si el PLC ya tiene el FC (sobrescribiéndolo); si no, en la de la plantilla.
                        var r = await tia.ImportBlocksAsync(new ImportBlocksArgs
                        {
                            Files =
                            {
                                new ImportFile
                                {
                                    Path = Path.Combine(temporal.Path, GeneradorMenus.NombreFc + ".xml"), Plc = plc,
                                    Folders = CarpetaFc.ToList(), UseExistingFolder = true
                                }
                            }
                        }, progreso);
                        importados += r.Succeeded;
                        fallos.AddRange(TextosTia.Fallos(r, importando: true));
                    }

                    var archivos = listas.Select(l => new HmiImportFile
                    {
                        Path = Path.Combine(temporal.Path, GeneradorMenus.CarpetaListas, l + ".xml"), Branch = HmiBranch.TextLists
                    }).ToList();
                    foreach (string hmi in hmis)
                    {
                        var r = await tia.ImportHmiAsync(new ImportHmiArgs { Hmi = hmi, Files = archivos }, progreso);
                        importados += r.Succeeded;
                        fallos.AddRange(TextosTia.Fallos(r).Select(f => Contexto.NombreVisible(hmi) + " · " + f));
                    }
                }

                // El FC nuevo se ve en el árbol común si ese PLC ya estaba leído.
                if (plc != null && Contexto.PlcLeido(plc) != null)
                    await Contexto.LeerPlcAsync(plc);

                Contexto.PonerFallos(fallos);
                string mensaje = L.T("{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos.", importados);
                Contexto.MostrarEstado(fallos.Count == 0 ? mensaje : mensaje + " " + L.T("{0} con error (ver abajo).", fallos.Count),
                    error: fallos.Count > 0);
            });
        }

        /// <summary>Carpeta del FC si el PLC aún no lo tiene (la de la plantilla SICAR@GST).</summary>
        private static readonly IReadOnlyList<string> CarpetaFc = new[] { "08_HMI_Management", "01_HMI_Configuration" };

        private bool PuedeImportarEnTia()
        {
            if (!PuedeUsarTia)
                return false;
            bool fc = GruposImportar[0].Componentes[0].Marcado && PlcDestino != null;
            bool listas = GruposImportar.Skip(1).Any(g => g.Componentes.Any(c => c.Marcado)) && HmisDestino.Any(h => h.Marcado);
            return fc || listas;
        }
        public ObservableCollection<HmiEditable> Hmis { get; } = new ObservableCollection<HmiEditable>();
        public ObservableCollection<PantallaFunciones> PantallasFunciones { get; } = new ObservableCollection<PantallaFunciones>();
        public ObservableCollection<DatasetProduccion> DatasetsProduccion { get; } = new ObservableCollection<DatasetProduccion>();

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

        public bool EsOem => SubgrupoActual?.Grupo == CatalogoMenus.Oem;
        public bool TieneNombres => SubgrupoActual?.Nombres > 0;
        public bool TieneCasillas => SubgrupoActual?.Posiciones > 0;

        public string NotaSubgrupo
        {
            get
            {
                var s = SubgrupoActual;
                if (s == null)
                    return "";
                if (s.Grupo == CatalogoMenus.ProductionData)
                    return L.T("Cada posición es un dataset; sus textos se escriben en la pestaña Production data.");
                if (s.Posiciones == 0)
                    return L.T("Solo nombres para la lista {0}: el FC no oculta estas pantallas.", s.Lista.Nombre);
                if (s.Nombres == 0)
                    return L.T("Sin lista de textos: solo se elige qué posiciones se ven en cada HMI.");
                string lista = s.Lista.Nombre + (s.Desplazamiento > 0 ? L.T(" (valores {0} a {1})", s.Desplazamiento + 1, s.Desplazamiento + s.Nombres) : "");
                return L.T("Nombres en la lista {0}. Si una pantalla no tiene nombre, sale el texto estándar.", lista);
            }
        }

        public bool HayNotaSubgrupo => SubgrupoActual != null;

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

            DatasetsProduccion.Clear();
            foreach (var tipo in CatalogoMenus.DatosProduccion)
                for (int dataset = 1; dataset <= CatalogoMenus.DatasetsProduccion; dataset++)
                    DatasetsProduccion.Add(new DatasetProduccion(tipo, dataset, proyecto, this));
            DatasetSeleccionado = DatasetsProduccion[0];

            if (NodoSeleccionado == null)
                NodoSeleccionado = Grupos[0].Hijos[0];
            else
                ReconstruirFilas();
            PantallaSeleccionada = PantallasFunciones[0];
            Cambiado = false;
        }

        // Al elegir un grupo la tabla sigue con el último subgrupo; al elegir un subgrupo, se cambia a él.
        partial void OnNodoSeleccionadoChanged(NodoMenu value)
        {
            if (value?.Subgrupo != null)
                SubgrupoActual = value.Subgrupo;
        }

        partial void OnSubgrupoActualChanged(SubgrupoMenu value) => ReconstruirFilas();

        partial void OnIdiomaChanged(IdiomaEdicion value)
        {
            OnPropertyChanged(nameof(IdiomaIndice));
            foreach (var fila in Filas.Where(f => f.Nombre != null))
                fila.Nombre.Refrescar();
            TituloOem?.Refrescar();
            foreach (var texto in PantallasFunciones.SelectMany(p => p.Funciones).SelectMany(f => f.Textos))
                texto.Refrescar();
            foreach (var fila in DatasetsProduccion.SelectMany(d => d.Filas))
                fila.Texto.Refrescar();
        }

        private void ReconstruirFilas()
        {
            var subgrupo = SubgrupoActual;
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
            foreach (var dataset in DatasetsProduccion)
                dataset.Refrescar();
            if (DatasetSeleccionado != null)
                foreach (var fila in DatasetSeleccionado.Filas)
                    fila.Texto.RefrescarMarca();
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
            var subgrupo = SubgrupoActual;
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
                var pares = textos.SelectMany(t => new[]
                {
                    string.IsNullOrWhiteSpace(t.Ingles) ? (t.Texto.Trim(), "en") : default,
                    string.IsNullOrWhiteSpace(t.Aleman) ? (t.Texto.Trim(), "de") : default
                }).Where(par => par.Item1 != null);
                var traducciones = await Traductor.TraducirVariosAsync(pares);

                // Una traducción igual al original (nombres de equipos, o fallo del traductor) se deja vacía: sale el texto.
                string Util(string origen, string destino) =>
                    traducciones.TryGetValue((origen, destino), out var t) && !string.Equals(t.Trim(), origen, StringComparison.OrdinalIgnoreCase) ? t.Trim() : null;
                foreach (var texto in textos)
                {
                    string origen = texto.Texto.Trim();
                    if (string.IsNullOrWhiteSpace(texto.Ingles))
                        texto.Ingles = Util(origen, "en") ?? "";
                    if (string.IsNullOrWhiteSpace(texto.Aleman))
                        texto.Aleman = Util(origen, "de") ?? "";
                }
                OnIdiomaChanged(Idioma);
                MarcarCambiado();
                Contexto.MostrarEstado(L.T("Traducidos {0} textos. Revísalos eligiendo Inglés o Alemán arriba.", textos.Count));
            });
        }

        private IEnumerable<TextoHmi> TodosLosTextos() =>
            _proyecto.TitulosOem
                .Concat(_proyecto.Nombres.Values.SelectMany(n => n))
                .Concat(_proyecto.FuncionesUsuario.SelectMany(p => p).SelectMany(f => f.Textos()))
                .Concat(_proyecto.DatosProduccion.Values.SelectMany(d => d).SelectMany(d => d));

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
            ImportarEnTiaCommand?.NotifyCanExecuteChanged();
        }
    }
}
