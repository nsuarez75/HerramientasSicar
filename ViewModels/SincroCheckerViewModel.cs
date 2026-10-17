using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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

    /// <summary>PERM or one of the two conflicting steps, with its ilock rungs.</summary>
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

    public partial class SincroCheckerViewModel : ObservableObject
    {
        private readonly TiaOpennessService _tiaService;
        private List<PasoSecuencia> _pasos = new List<PasoSecuencia>();
        private SincroChecker _checker = new SincroChecker();
        private bool _cargandoSenales;
        private bool _cargandoBloque;
        private HashSet<string> _senalesDiferencia = new HashSet<string>();

        [ObservableProperty]
        private string _rutaXml = "";

        [ObservableProperty]
        private string _rutaProyecto = "";

        [ObservableProperty]
        private string _nombreBloque = "";

        [ObservableProperty]
        private string _estado = "Abre un FB de secuencia para comprobar la sincronización";

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _excluirHomeWork = true;

        [ObservableProperty]
        private bool _excluirValoresDecodificados = true;

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
        private bool _hayConflictoActivo;

        public ObservableCollection<ConflictoSincro> Conflictos { get; } = new ObservableCollection<ConflictoSincro>();
        public ObservableCollection<SeccionPaso> Secciones { get; } = new ObservableCollection<SeccionPaso>();
        public ObservableCollection<SenalSincro> Senales { get; } = new ObservableCollection<SenalSincro>();
        public ObservableCollection<GrupoDiferencias> Diferencias { get; } = new ObservableCollection<GrupoDiferencias>();

        public SincroCheckerViewModel(TiaOpennessService tiaService)
        {
            _tiaService = tiaService;
        }

        partial void OnExcluirHomeWorkChanged(bool value) => Comprobar();
        partial void OnExcluirValoresDecodificadosChanged(bool value) => Comprobar();

        partial void OnPrimerPasoManualChanged(string value)
        {
            if (_cargandoBloque)
                return;

            PedirPrimerManual = false;
            if (!LeerPrimerPasoManual(out _))
            {
                // Keep the previous result until the value is valid
                AvisoPrimerManual = "Formato no válido: usa el número de paso, p.ej. S100";
                return;
            }

            AvisoPrimerManual = "";
            Comprobar();
        }
        partial void OnConflictoSeleccionadoChanged(ConflictoSincro value) => CargarConflicto(value);

        [RelayCommand]
        private async Task BuscarXmlAsync()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Bloque exportado (*.xml)|*.xml|Todos los archivos (*.*)|*.*";
            ofd.Title = "Seleccionar FB de secuencia exportado";

            if (ofd.ShowDialog() != true)
                return;

            string ruta = ofd.FileName;
            string tipo = null;
            try { tipo = LadSequenceAnalyzer.TipoObjetoRaiz(ruta); } catch { /* the analyzer reports invalid XML */ }

            // A tag table or DB of the station was chosen: offer its sequence FB from the same folder
            if (tipo != null && tipo != "SW.Blocks.FB")
            {
                var fbs = LadSequenceAnalyzer.BuscarFbsSecuenciaAsociados(ruta);
                string descripcion = "El archivo seleccionado es " + LadSequenceAnalyzer.DescribirTipo(tipo) + ", no un FB de secuencia.";

                if (fbs.Count == 1)
                {
                    if (MessageBox.Show(descripcion + "\n\nEn la misma carpeta está el FB de secuencia:\n" + Path.GetFileName(fbs[0]) + "\n\n¿Quieres abrirlo?",
                            "Sincro Checker", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        ruta = fbs[0];
                }
                else if (fbs.Count > 1)
                {
                    MessageBox.Show(descripcion + "\n\nEsta estación tiene " + fbs.Count + " secuencias:\n" +
                            string.Join("\n", fbs.Select(Path.GetFileName)) + "\n\nElige cuál abrir.",
                        "Sincro Checker", MessageBoxButton.OK, MessageBoxImage.Information);

                    string nombre = Path.GetFileNameWithoutExtension(ruta);
                    OpenFileDialog elegir = new OpenFileDialog();
                    elegir.Title = "Seleccionar secuencia de " + nombre;
                    elegir.InitialDirectory = Path.GetDirectoryName(ruta);
                    elegir.Filter = "Secuencias de " + nombre + "|FB*" + nombre + "*_FB.xml|Todos los XML (*.xml)|*.xml";
                    if (elegir.ShowDialog() != true)
                        return;
                    ruta = elegir.FileName;
                }
            }

            RutaXml = ruta;
            IsProcessing = true;
            Estado = "Analizando bloque...";
            await Task.Run(() => Analizar(RutaXml));
        }

        [RelayCommand]
        private void BuscarProyecto()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "TIA Portal Project (*.ap15;*.ap15_1;*.ap16;*.ap17;*.ap18;*.ap19)|*.ap15;*.ap15_1;*.ap16;*.ap17;*.ap18;*.ap19|Todos los archivos (*.*)|*.*";
            ofd.Title = "Seleccionar proyecto TIA (.ap19)";

            if (ofd.ShowDialog() == true)
                RutaProyecto = ofd.FileName;
        }

        [RelayCommand]
        private async Task ExportarDesdeTiaAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaProyecto) || !File.Exists(RutaProyecto))
            {
                MessageBox.Show("Selecciona un proyecto válido (.apXX)",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(NombreBloque))
            {
                MessageBox.Show("Introduce el nombre del bloque a exportar.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Detect TIA version automatically
            if (!TiaVersionResolver.DetectarDesdeProyecto(RutaProyecto))
            {
                MessageBox.Show(
                    "No se pudo detectar qué versión de TIA Portal corresponde a este proyecto.",
                    "TIA no encontrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsProcessing = true;
            Estado = $"Cargando TIA Portal v{TiaVersionResolver.TiaVersionDetected}…";

            await Task.Run(() =>
            {
                try
                {
                    string pathExportar = Path.GetDirectoryName(RutaProyecto);

                    _tiaService.AbrirProyecto(RutaProyecto);

                    Estado = "Compilando bloque...";
                    _tiaService.CompilarBloque(NombreBloque);

                    Estado = "Exportando bloque...";
                    _tiaService.ExportarBloque(NombreBloque, pathExportar);

                    RutaXml = Path.Combine(pathExportar, NombreBloque + ".xml");
                }
                catch (Exception ex)
                {
                    MostrarError(ex);
                    Estado = "Listo";
                    IsProcessing = false;
                    return;
                }

                Estado = "Analizando bloque...";
                Analizar(RutaXml);
            });
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

        private void Analizar(string rutaXml)
        {
            try
            {
                var pasos = new LadSequenceAnalyzer().AnalizarBloque(rutaXml);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _pasos = pasos;

                    // From the first manual step onwards all steps are manual; ask when it cannot be detected
                    var primerManual = SincroChecker.DetectarPrimerPasoManual(pasos);
                    _cargandoBloque = true;
                    PrimerPasoManual = primerManual.HasValue ? "S" + primerManual.Value.ToString("000") : "";
                    _cargandoBloque = false;
                    PedirPrimerManual = !primerManual.HasValue;
                    AvisoPrimerManual = primerManual.HasValue
                        ? "Detectado automáticamente"
                        : "No se ha detectado ningún paso manual. Si la secuencia tiene pasos manuales, indica el primero; si no, déjalo vacío.";

                    Comprobar();
                });
            }
            catch (Exception ex)
            {
                MostrarError(ex);
                Estado = "Listo";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void Comprobar()
        {
            LeerPrimerPasoManual(out int? primerManual);
            _checker = new SincroChecker
            {
                ExcluirHomeWork = ExcluirHomeWork,
                ExcluirValoresDecodificados = ExcluirValoresDecodificados,
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
            if (nPasos == 0)
                Estado = "El bloque no tiene pasos automáticos (S###)";
            else if (conflictos.Count == 0)
                Estado = $"✔ {nPasos} pasos automáticos: ningún par de pasos puede sincronizar a la vez";
            else
                Estado = $"✖ {nPasos} pasos automáticos: {conflictos.Count} pares de pasos pueden sincronizar a la vez";
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

            var perm = _checker.Perm;
            if (perm != null && (perm.ExpresionIlockAuto != null || perm.ExpresionIlockExtSync != null))
                Secciones.Add(CrearSeccion(perm, "PERM", "Se ejecuta en cada ciclo junto al paso activo"));
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
                    Titulo = g.Key.Exige + " siempre exige" + (g.Key.OtroLaComprueba
                        ? " (" + g.Key.Otro + " también las usa, pero admite el valor contrario):"
                        : " (" + g.Key.Otro + " no las comprueba):"),
                    Senales = string.Join(", ", g.Select(d => d.Senal.Replace("\"", "") + "=" + (d.Valor ? "1" : "0")))
                });
            }

            ActualizarEstado();
        }

        private SeccionPaso CrearSeccion(PasoSecuencia paso, string titulo, string subtitulo)
        {
            var segmentos = new List<SegmentoLad>();
            // A step that does not write ilockAuto leaves it at 1
            segmentos.Add(paso.ExpresionIlockAuto != null
                ? new SegmentoLad { Bobina = "ilockAuto", Expresion = paso.ExpresionIlockAuto }
                : new SegmentoLad { Bobina = "ilockAuto (no escrito → 1)", Expresion = ExpresionLogica.True });
            if (paso.ExpresionIlockExtSync != null)
                segmentos.Add(new SegmentoLad { Bobina = "ilockExtSync", Expresion = paso.ExpresionIlockExtSync });

            return new SeccionPaso(titulo, subtitulo, segmentos, estado =>
                (paso.ExpresionIlockAuto ?? ExpresionLogica.True).Evaluar(estado) &&
                (paso.ExpresionIlockExtSync ?? ExpresionLogica.True).Evaluar(estado));
        }

        private void ActualizarEstado()
        {
            if (_cargandoSenales)
                return;

            var estado = Senales.ToDictionary(s => s.Nombre, s => s.Valor);
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
            ResultadoSincro = a && b ? $"Con este estado sincronizan {c.PasoA.Paso} y {c.PasoB.Paso} a la vez → error de sincronización"
                : a ? $"Con este estado solo sincroniza {c.PasoA.Paso}"
                : b ? $"Con este estado solo sincroniza {c.PasoB.Paso}"
                : "Con este estado no sincroniza ninguno de los dos pasos";

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
                    ? "Ninguna señal por sí sola evita la coincidencia: hay que cambiar varias a la vez."
                    : "Ninguna señal por sí sola provoca la coincidencia.";
            }
            else if (ambos)
            {
                var lineas = new List<string>();
                if (comunes.Count > 0)
                    lineas.Add("Las exigen los dos pasos: " + Listar(comunes));
                if (soloA.Count > 0)
                    lineas.Add("Solo las exige " + c.PasoA.Paso + ": " + Listar(soloA));
                if (soloB.Count > 0)
                    lineas.Add("Solo las exige " + c.PasoB.Paso + ": " + Listar(soloB));
                ResumenClave = string.Join("\n", lineas);
            }
            else
            {
                ResumenClave = "Cambiando una sola de estas señales coincidirían: " + Listar(soloA.Concat(soloB).Concat(comunes));
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

        private static void MostrarError(Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}
