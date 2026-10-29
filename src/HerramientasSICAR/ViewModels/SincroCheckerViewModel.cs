using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>A signal involved in a conflict; its value can be changed from the LAD view.</summary>
    public partial class SenalSincro : ObservableObject
    {
        private readonly Action _alCambiar;

        public string Nombre { get; }

        [ObservableProperty]
        private bool _valor;

        /// <summary>Changing only this signal changes whether both steps synchronize at once.</summary>
        [ObservableProperty]
        private bool _clave;

        public SenalSincro(string nombre, bool valor, Action alCambiar)
        {
            Nombre = nombre;
            _valor = valor;
            _alCambiar = alCambiar;
        }

        partial void OnValorChanged(bool value) => _alCambiar();
    }

    /// <summary>Signals one step always requires and the other does not, as a fact (no proposal).</summary>
    public class GrupoDiferencias
    {
        public string Titulo { get; set; }
        public string Senales { get; set; }
    }

    public class SegmentoLad
    {
        public string Bobina { get; set; }
        public ExpresionLogica Expresion { get; set; }
    }

    /// <summary>One of the two conflicting steps, with its ilock rungs.</summary>
    public partial class SeccionPaso : ObservableObject
    {
        private readonly Func<IDictionary<string, bool>, bool> _evaluar;

        public string Titulo { get; }
        public string Subtitulo { get; }
        public List<SegmentoLad> Segmentos { get; }

        [ObservableProperty]
        private bool _cumple;

        public SeccionPaso(string titulo, string subtitulo, List<SegmentoLad> segmentos, Func<IDictionary<string, bool>, bool> evaluar)
        {
            Titulo = titulo;
            Subtitulo = subtitulo;
            Segmentos = segmentos;
            _evaluar = evaluar;
        }

        public void Actualizar(IDictionary<string, bool> estado) => Cumple = _evaluar(estado);
    }

    public partial class SincroCheckerViewModel : Herramienta
    {
        private List<PasoSecuencia> _pasos = new List<PasoSecuencia>();
        private SincroChecker _checker = new SincroChecker();
        private bool _cargandoSenales;
        private bool _cargandoBloque;
        private HashSet<string> _senalesDiferencia = new HashSet<string>();

        public SincroCheckerViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => "Sincro Checker";
        public override string Descripcion => L.T("Detecta pares de pasos de un FB de secuencia (LAD) cuyos ilocks (ilockAuto e ilockExtSync, sin transAuto ni salto activos) pueden cumplirse a la vez al pasar de manual a automático, lo que da error de sincronización.");
        public override string Icono => "git-compare";

        // Puede abrir un XML ya exportado sin conexión; solo «Exportar desde TIA» necesita el proyecto.
        public override bool UsaTia => false;

        [ObservableProperty]
        private string _rutaXml = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportarDesdeTiaCommand))]
        private string _nombreBloque = "";

        /// <summary>Resultado de la comprobación del bloque abierto.</summary>
        [ObservableProperty]
        private string _resumen = "";

        [ObservableProperty]
        private bool _hayResumen;

        [ObservableProperty]
        private bool _resumenConConflictos;

        [ObservableProperty]
        private bool _excluirHomeWork = true;

        [ObservableProperty]
        private bool _excluirValoresDecodificados = true;

        /// <summary>Robot sequence: S223–S227 are not checked.</summary>
        [ObservableProperty]
        private bool _esRobot;

        /// <summary>First manual step ("S100"); from it onwards all steps are manual. Empty = none.</summary>
        [ObservableProperty]
        private string _primerPasoManual = "";

        [ObservableProperty]
        private string _avisoPrimerManual = "";

        [ObservableProperty]
        private bool _pedirPrimerManual;

        [ObservableProperty]
        private ConflictoSincro _conflictoSeleccionado;

        [ObservableProperty]
        private IDictionary<string, bool> _estadoSenales = new Dictionary<string, bool>();

        [ObservableProperty]
        private string _resultadoSincro = "";

        [ObservableProperty]
        private ISet<string> _senalesClave = new HashSet<string>();

        [ObservableProperty]
        private string _resumenClave = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SinDiferencias))]
        private bool _hayConflictoActivo;

        public bool SinDiferencias => HayConflictoActivo && Diferencias.Count == 0;
        public bool MostrarPrimerManual => HayResumen && !PedirPrimerManual;

        public ObservableCollection<ConflictoSincro> Conflictos { get; } = new ObservableCollection<ConflictoSincro>();
        public ObservableCollection<SeccionPaso> Secciones { get; } = new ObservableCollection<SeccionPaso>();
        public ObservableCollection<SenalSincro> Senales { get; } = new ObservableCollection<SenalSincro>();
        public ObservableCollection<GrupoDiferencias> Diferencias { get; } = new ObservableCollection<GrupoDiferencias>();

        partial void OnExcluirHomeWorkChanged(bool value) => Comprobar();
        partial void OnExcluirValoresDecodificadosChanged(bool value) => Comprobar();
        partial void OnEsRobotChanged(bool value) => Comprobar();
        partial void OnHayResumenChanged(bool value) => OnPropertyChanged(nameof(MostrarPrimerManual));
        partial void OnPedirPrimerManualChanged(bool value) => OnPropertyChanged(nameof(MostrarPrimerManual));

        partial void OnPrimerPasoManualChanged(string value)
        {
            if (_cargandoBloque)
                return;

            PedirPrimerManual = false;
            if (!LeerPrimerPasoManual(out _))
            {
                // Keep the previous result until the value is valid
                AvisoPrimerManual = L.T("Formato no válido: usa el número de paso, p.ej. S100");
                return;
            }

            AvisoPrimerManual = "";
            Comprobar();
        }
        partial void OnConflictoSeleccionadoChanged(ConflictoSincro value) => CargarConflicto(value);

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task AbrirXml()
        {
            string ruta = await Contexto.Dialogos.ElegirArchivo(L.T("FB de secuencia exportado"),
                string.IsNullOrEmpty(RutaXml) ? null : Path.GetDirectoryName(RutaXml), TipoArchivo.Xml);
            if (ruta == null)
                return;

            string tipo = null;
            try { tipo = LadSequenceAnalyzer.TipoObjetoRaiz(ruta); } catch { /* the analyzer reports invalid XML */ }

            // A tag table or DB of the station was chosen: offer its sequence FB from the same folder
            if (tipo != null && tipo != "SW.Blocks.FB")
            {
                var fbs = LadSequenceAnalyzer.BuscarFbsSecuenciaAsociados(ruta);
                string descripcion = L.T("El archivo elegido es {0}, no un FB de secuencia.", LadSequenceAnalyzer.DescribirTipo(tipo));

                if (fbs.Count == 1)
                {
                    if (await Contexto.Dialogos.Confirmar("Sincro Checker",
                            descripcion + "\n\n" + L.T("En la misma carpeta está el FB de secuencia:\n{0}\n\n¿Quieres abrirlo?", Path.GetFileName(fbs[0])),
                            L.T("Abrir")))
                        ruta = fbs[0];
                }
                else if (fbs.Count > 1)
                {
                    await Contexto.Dialogos.Mensaje("Sincro Checker",
                        descripcion + "\n\n" + L.T("Esta estación tiene {0} secuencias:\n{1}\n\nElige cuál abrir.",
                            fbs.Count, string.Join("\n", fbs.Select(Path.GetFileName))));

                    string nombre = Path.GetFileNameWithoutExtension(ruta);
                    ruta = await Contexto.Dialogos.ElegirArchivo(L.T("Secuencia de {0}", nombre), Path.GetDirectoryName(ruta),
                        new TipoArchivo(L.T("Secuencias de {0}", nombre), "FB*" + nombre + "*_FB.xml"), TipoArchivo.Xml);
                    if (ruta == null)
                        return;
                }
            }

            await Analizar(ruta);
        }

        [RelayCommand(CanExecute = nameof(PuedeExportar))]
        private async Task ExportarDesdeTia()
        {
            string bloque = NombreBloque.Trim();
            string xml = null;
            await Contexto.Trabajar(L.T("Exportando {0}…", bloque), async () =>
            {
                xml = await Contexto.Tia().EnProyecto(p =>
                {
                    p.CompilarBloque(bloque);
                    return p.ExportarBloque(bloque, p.Carpeta);
                });
            });
            if (xml != null)
                await Analizar(xml);
        }

        private bool PuedeExportar() => PuedeUsarTia && !string.IsNullOrWhiteSpace(NombreBloque);

        protected override void ActualizarComandos()
        {
            AbrirXmlCommand.NotifyCanExecuteChanged();
            ExportarDesdeTiaCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void ToggleSenal(string nombre)
        {
            var senal = Senales.FirstOrDefault(s => s.Nombre == nombre);
            if (senal != null)
                senal.Valor = !senal.Valor;
        }

        [RelayCommand]
        private void RestaurarEstado() => CargarConflicto(ConflictoSeleccionado);

        private async Task Analizar(string rutaXml)
        {
            RutaXml = rutaXml;
            List<PasoSecuencia> pasos = null;
            await Contexto.Trabajar(L.T("Analizando el bloque…"), async () =>
            {
                pasos = await Task.Run(() => new LadSequenceAnalyzer().AnalizarBloque(rutaXml));
            });
            if (pasos == null)
                return;

            _pasos = pasos;

            // From the first manual step onwards all steps are manual; ask when it cannot be detected
            var primerManual = SincroChecker.DetectarPrimerPasoManual(pasos);
            _cargandoBloque = true;
            PrimerPasoManual = primerManual.HasValue ? "S" + primerManual.Value.ToString("000") : "";
            _cargandoBloque = false;
            PedirPrimerManual = !primerManual.HasValue;
            AvisoPrimerManual = primerManual.HasValue
                ? L.T("Detectado automáticamente")
                : L.T("No se ha detectado ningún paso manual. Si la secuencia tiene pasos manuales, indica el primero; si no, déjalo vacío.");

            Comprobar();
        }

        private void Comprobar()
        {
            LeerPrimerPasoManual(out int? primerManual);
            _checker = new SincroChecker
            {
                ExcluirHomeWork = ExcluirHomeWork,
                ExcluirValoresDecodificados = ExcluirValoresDecodificados,
                EsRobot = EsRobot,
                PrimerPasoManual = primerManual
            };
            var conflictos = _checker.Comprobar(_pasos);

            Conflictos.Clear();
            foreach (var c in conflictos)
                Conflictos.Add(c);
            ConflictoSeleccionado = Conflictos.FirstOrDefault();

            if (_pasos.Count == 0)
                return;

            int nPasos = _checker.PasosAuto.Count;
            Resumen = nPasos == 0 ? L.T("El bloque no tiene pasos automáticos (S###)")
                : conflictos.Count == 0 ? L.T("{0} pasos automáticos: ningún par de pasos puede sincronizar a la vez", nPasos)
                : L.T("{0} pasos automáticos: {1} pares de pasos pueden sincronizar a la vez", nPasos, conflictos.Count);
            ResumenConConflictos = conflictos.Count > 0;
            HayResumen = true;
            Contexto.MostrarEstado(Path.GetFileName(RutaXml) + ": " + Resumen);
        }

        private void CargarConflicto(ConflictoSincro conflicto)
        {
            Secciones.Clear();
            Senales.Clear();
            Diferencias.Clear();
            HayConflictoActivo = conflicto != null;
            if (conflicto == null)
            {
                EstadoSenales = new Dictionary<string, bool>();
                SenalesClave = new HashSet<string>();
                ResultadoSincro = "";
                ResumenClave = "";
                return;
            }

            Secciones.Add(CrearSeccion(conflicto.PasoA, conflicto.PasoA.Paso, conflicto.PasoA.Titulo));
            Secciones.Add(CrearSeccion(conflicto.PasoB, conflicto.PasoB.Paso, conflicto.PasoB.Titulo));

            var variables = new HashSet<string>();
            _checker.CondicionSincro(conflicto.PasoA).RecogerVariables(variables);
            _checker.CondicionSincro(conflicto.PasoB).RecogerVariables(variables);

            _cargandoSenales = true;
            foreach (var v in variables.OrderBy(v => v))
                Senales.Add(new SenalSincro(v, conflicto.Estado.TryGetValue(v, out bool valor) && valor, ActualizarEstado));
            _cargandoSenales = false;

            var diferencias = _checker.Diferencias(conflicto);
            _senalesDiferencia = new HashSet<string>(diferencias.Select(d => d.Senal));
            foreach (var g in diferencias
                         .GroupBy(d => new { Exige = d.PasoQueExige.Paso, Otro = d.OtroPaso.Paso, d.OtroLaComprueba })
                         .OrderBy(g => g.Key.Exige == conflicto.PasoA.Paso ? 0 : 1)
                         .ThenBy(g => g.Key.OtroLaComprueba))
            {
                Diferencias.Add(new GrupoDiferencias
                {
                    Titulo = g.Key.OtroLaComprueba
                        ? L.T("{0} siempre exige ({1} también las usa, pero admite el valor contrario):", g.Key.Exige, g.Key.Otro)
                        : L.T("{0} siempre exige ({1} no las comprueba):", g.Key.Exige, g.Key.Otro),
                    Senales = string.Join(", ", g.Select(d => d.Senal.Replace("\"", "") + "=" + (d.Valor ? "1" : "0")))
                });
            }

            OnPropertyChanged(nameof(SinDiferencias));
            ActualizarEstado();
        }

        private SeccionPaso CrearSeccion(PasoSecuencia paso, string titulo, string subtitulo)
        {
            var segmentos = new List<SegmentoLad>();
            // A step that does not write ilockAuto leaves it at 1
            segmentos.Add(paso.ExpresionIlockAuto != null
                ? new SegmentoLad { Bobina = "ilockAuto", Expresion = paso.ExpresionIlockAuto }
                : new SegmentoLad { Bobina = L.T("ilockAuto (no escrito → 1)"), Expresion = ExpresionLogica.True });
            if (paso.ExpresionIlockExtSync != null)
                segmentos.Add(new SegmentoLad { Bobina = "ilockExtSync", Expresion = paso.ExpresionIlockExtSync });
            // With transAuto at 1 the step can advance, so it is not proposed for synchronization
            if (paso.ExpresionTransAuto != null)
                segmentos.Add(new SegmentoLad { Bobina = L.T("transAuto (a 1 no sincroniza)"), Expresion = paso.ExpresionTransAuto });
            // Same for a jump to another step (MOVE to #Sequence.stepNplus1)
            foreach (var salto in paso.Saltos)
            {
                string destino = int.TryParse(salto.Valor, out int n) ? "S" + n.ToString("000") : salto.Valor;
                segmentos.Add(new SegmentoLad { Bobina = L.T("Salto a {0} (a 1 no sincroniza)", destino), Expresion = salto.Condicion });
            }

            return new SeccionPaso(titulo, subtitulo, segmentos, estado => _checker.CondicionSincro(paso).Evaluar(estado));
        }

        private void ActualizarEstado()
        {
            if (_cargandoSenales)
                return;

            var estado = Senales.ToDictionary(s => s.Nombre, s => s.Valor);
            // Constants (pos_Pulse_OM_Sync = 1 in the sync scan...) are not editable, but the LAD shows their value
            var variables = new HashSet<string>();
            foreach (var segmento in Secciones.SelectMany(s => s.Segmentos))
                segmento.Expresion.RecogerVariables(variables);
            foreach (var v in variables)
            {
                bool? fijo = SincroChecker.ValorFijo(v);
                if (fijo.HasValue)
                    estado[v] = fijo.Value;
            }
            EstadoSenales = estado;
            foreach (var s in Secciones)
                s.Actualizar(estado);

            var c = ConflictoSeleccionado;
            if (c == null)
                return;

            var condicionA = _checker.CondicionSincro(c.PasoA);
            var condicionB = _checker.CondicionSincro(c.PasoB);
            bool a = condicionA.Evaluar(estado);
            bool b = condicionB.Evaluar(estado);
            ResultadoSincro = a && b ? L.T("Con este estado sincronizan {0} y {1} a la vez → error de sincronización", c.PasoA.Paso, c.PasoB.Paso)
                : a ? L.T("Con este estado solo sincroniza {0}", c.PasoA.Paso)
                : b ? L.T("Con este estado solo sincroniza {0}", c.PasoB.Paso)
                : L.T("Con este estado no sincroniza ninguno de los dos pasos");

            // Key signals: flipping only that one changes whether both steps synchronize at once
            bool ambos = a && b;
            var clave = new HashSet<string>();
            var comunes = new List<SenalSincro>();
            var soloA = new List<SenalSincro>();
            var soloB = new List<SenalSincro>();
            foreach (var s in Senales)
            {
                var invertido = new Dictionary<string, bool>(estado) { [s.Nombre] = !s.Valor };
                bool a2 = condicionA.Evaluar(invertido);
                bool b2 = condicionB.Evaluar(invertido);
                if ((a2 && b2) == ambos)
                    continue;

                clave.Add(s.Nombre);
                if (a != a2 && b != b2) comunes.Add(s);
                else if (a != a2) soloA.Add(s);
                else soloB.Add(s);
            }

            foreach (var s in Senales)
                s.Clave = clave.Contains(s.Nombre);

            // In the LAD only the signals that differ between both steps are highlighted
            SenalesClave = _senalesDiferencia;

            if (clave.Count == 0)
            {
                ResumenClave = ambos
                    ? L.T("Ninguna señal por sí sola evita la coincidencia: hay que cambiar varias a la vez.")
                    : L.T("Ninguna señal por sí sola provoca la coincidencia.");
            }
            else if (ambos)
            {
                var lineas = new List<string>();
                if (comunes.Count > 0)
                    lineas.Add(L.T("Las exigen los dos pasos: {0}", Listar(comunes)));
                if (soloA.Count > 0)
                    lineas.Add(L.T("Solo las exige {0}: {1}", c.PasoA.Paso, Listar(soloA)));
                if (soloB.Count > 0)
                    lineas.Add(L.T("Solo las exige {0}: {1}", c.PasoB.Paso, Listar(soloB)));
                ResumenClave = string.Join("\n", lineas);
            }
            else
            {
                ResumenClave = L.T("Cambiando una sola de estas señales coincidirían: {0}", Listar(soloA.Concat(soloB).Concat(comunes)));
            }
        }

        private static string Listar(IEnumerable<SenalSincro> senales) =>
            string.Join(", ", senales.Select(s => s.Nombre.Replace("\"", "") + "=" + (s.Valor ? "1" : "0")));

        /// <summary>Accepts "S100", "100" or empty (no manual steps).</summary>
        private bool LeerPrimerPasoManual(out int? numero)
        {
            numero = null;
            string texto = (PrimerPasoManual ?? "").Trim().TrimStart('S', 's');
            if (texto == "")
                return true;
            if (!int.TryParse(texto, out int n))
                return false;
            numero = n;
            return true;
        }
    }
}
