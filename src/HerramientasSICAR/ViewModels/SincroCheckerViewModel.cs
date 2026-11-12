using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Programs;
using HerramientasSICAR.Nucleo.Sicar;
using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Secuencia con pares de pasos que pueden sincronizar a la vez.</summary>
    public sealed class FilaSecuencia
    {
        public FilaSecuencia(SequenceBlock secuencia, string zona, IReadOnlyList<FilaPar> pares)
        {
            Secuencia = secuencia;
            Zona = zona;
            Pares = pares;
        }

        public SequenceBlock Secuencia { get; }
        public string Zona { get; }
        public IReadOnlyList<FilaPar> Pares { get; }
        public string Texto => $"{Zona}  ·  {Secuencia.Sequence}  ·  {Secuencia.Name}";
        public string Cantidad => Pares.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class FilaPar
    {
        public FilaPar(SyncConflict conflicto, string pasoA, string pasoB)
        {
            Conflicto = conflicto;
            PasoA = pasoA;
            PasoB = pasoB;
        }

        public SyncConflict Conflicto { get; }
        public string PasoA { get; }
        public string PasoB { get; }
        public string Par => $"{Conflicto.StepA.Label} ↔ {Conflicto.StepB.Label}";
    }

    /// <summary>Señal que se puede marcar como excluyente en un par.</summary>
    public sealed partial class FilaCandidata : ObservableObject
    {
        public FilaCandidata(string senal, string comentario)
        {
            Senal = senal;
            Comentario = comentario;
        }

        public string Senal { get; }
        public string Comentario { get; }
        public string Nombre => TextoSenal.Nombre(Senal);

        [ObservableProperty]
        private bool _marcada;
    }

    public sealed class FilaGrupo
    {
        public FilaGrupo(ExclusiveGroup grupo)
        {
            Grupo = grupo;
        }

        public ExclusiveGroup Grupo { get; }
        public string Texto => string.Join("  ·  ", Grupo.Signals.Select(TextoSenal.Nombre));
        public string Nota => Grupo.Note ?? "";
    }

    /// <summary>Las señales del análisis van con el texto del operando: "Variable"."Miembro".</summary>
    public static class TextoSenal
    {
        private const string Separador = "\".\"";

        public static string[] Componentes(string senal) => senal.Trim('"').Split(new[] { Separador }, StringSplitOptions.None);

        public static string Nombre(string senal) => string.Join(".", Componentes(senal));
    }

    /// <summary>
    /// Doble sincronización de un PLC, con la misma comprobación que GST-Diag (Nucleo/Sicar/SyncChecker.cs): pares de
    /// pasos que pueden sincronizar a la vez y decisiones del usuario sobre señales excluyentes. Al exportar desde TIA,
    /// el programa va a %TEMP% y las decisiones a AppData (<see cref="ArchivoSincro.EnAppData"/>); al abrir una carpeta
    /// exportada a mano, al sincro.json de esa carpeta.
    /// </summary>
    public partial class SincroCheckerViewModel : Herramienta
    {
        private PlcProgram _programa;
        private ArchivoSincro _archivo = new ArchivoSincro();

        public SincroCheckerViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => "Sincro Checker";
        public override string Descripcion => L.T("Busca en el programa de un PLC los pares de pasos de cada secuencia que pueden ser candidatos a la vez al pasar de manual a automático, lo que da error de sincronización. Usa las mismas reglas que GST-Diag.");
        public override string Icono => "git-compare";

        // Puede abrir una carpeta ya exportada sin conexión; solo «Exportar desde TIA» necesita el proyecto.
        public override bool UsaTia => false;

        public ObservableCollection<FilaSecuencia> Secuencias { get; } = new ObservableCollection<FilaSecuencia>();
        public ObservableCollection<FilaPar> Pares { get; } = new ObservableCollection<FilaPar>();
        public ObservableCollection<FilaCandidata> Candidatas { get; } = new ObservableCollection<FilaCandidata>();
        public ObservableCollection<FilaGrupo> Grupos { get; } = new ObservableCollection<FilaGrupo>();

        /// <summary>Carpeta del programa exportado que se está analizando.</summary>
        [ObservableProperty]
        private string _carpeta = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayPrograma))]
        private bool _cargando;

        [ObservableProperty]
        private string _resumen = "";

        [ObservableProperty]
        private bool _resumenConPares;

        [ObservableProperty]
        private FilaSecuencia _secuenciaSeleccionada;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayPar), nameof(SinCandidatas))]
        private FilaPar _parSeleccionado;

        public bool HayPrograma => _programa != null && !Cargando;
        public bool HayPares => HayPrograma && Secuencias.Count > 0;
        public bool HayPar => ParSeleccionado != null;
        public bool HayCandidatas => Candidatas.Count > 0;
        public bool SinCandidatas => HayPar && Candidatas.Count == 0;
        public bool HayGrupos => HayPrograma && Grupos.Count > 0;
        public string TituloGrupos => L.T("Señales excluyentes guardadas ({0})", Grupos.Count);

        [RelayCommand(CanExecute = nameof(Libre))]
        private async Task AbrirCarpeta()
        {
            string carpeta = await Contexto.Dialogos.ElegirCarpeta(L.T("Carpeta con el programa exportado del PLC"),
                string.IsNullOrEmpty(_carpetaAbierta) ? null : _carpetaAbierta);
            if (carpeta == null)
                return;
            _carpetaAbierta = carpeta;
            await Cargar(carpeta, carpeta, ArchivoSincro.EnCarpeta(carpeta), null);
        }

        // Última carpeta abierta a mano, para empezar ahí el diálogo.
        private string _carpetaAbierta;

        // Dónde se guardan las decisiones del programa cargado.
        private string _rutaArchivo;

        [RelayCommand(CanExecute = nameof(PuedeUsarTia))]
        private async Task ExportarDesdeTia()
        {
            string plc = await ElegirPlc(L.T("Exportar desde TIA"), L.T("Exportar"));
            if (plc == null)
                return;

            // Se exporta a %TEMP% y se borra en cuanto se ha leído: en la carpeta del proyecto no queda nada.
            using (var temporal = new TempFolder())
            {
                var info = Contexto.Proyecto;
                bool exportado = false;
                var saltados = new List<string>();
                await Contexto.Trabajar(L.T("Exportando el programa de {0}…", plc), async () =>
                {
                    // Bloques, tablas de variables y tipos, sin compilar nada: lo protegido o sin compilar se salta.
                    var resultado = await (await Contexto.TiaAsync()).ExportProgramAsync(plc, temporal.Path, Contexto.ProgresoTia());
                    saltados.AddRange(TextosTia.Fallos(resultado));
                    exportado = !resultado.Stopped;
                    if (!exportado)
                    {
                        Contexto.PonerFallos(saltados);
                        Contexto.MostrarEstado(L.T("TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer.", resultado.Remaining), error: true);
                    }
                });
                if (!exportado)
                    return;

                // Antes la exportación y el sincro.json se dejaban en la carpeta del proyecto.
                string anterior = ArchivoSincro.EnCarpeta(Path.Combine(info.ProjectFolder, "HerramientasSICAR", "Sincro", FileRules.SafeName(plc)));
                string ruta = ArchivoSincro.EnAppData(info.Name, plc);
                TraerExcluyentesAnteriores(anterior, ruta);
                await Cargar(temporal.Path, L.T("{0} · {1} (exportado de TIA)", info.Name, plc), ruta, info.EditingLanguage);
                if (saltados.Count > 0 && HayPrograma)
                {
                    Contexto.PonerFallos(saltados.Concat(_programa.Warnings));
                    Contexto.MostrarEstado(L.T("{0}. {1} objeto(s) no se han exportado (ver abajo): sus señales cuentan como libres.", Resumen, saltados.Count), error: true);
                }
            }
        }

        // Las excluyentes del sincro.json que dejaban las versiones anteriores en la carpeta del proyecto, si aún no hay
        // archivo en AppData.
        private static void TraerExcluyentesAnteriores(string anterior, string ruta)
        {
            try
            {
                if (File.Exists(ruta) || !File.Exists(anterior))
                    return;
                ArchivoSincro.Cargar(anterior).Guardar(ruta);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Se empieza sin decisiones.
            }
        }

        // Lee el programa de la carpeta y calcula los pares; las decisiones se leen y se guardan en rutaArchivo. El idioma
        // de edición viene de TIA al exportar; al abrir una carpeta, del sincro.json que se dejó entonces.
        private async Task Cargar(string carpeta, string origen, string rutaArchivo, string idiomaEdicion)
        {
            _programa = null;
            Carpeta = origen;
            _rutaArchivo = rutaArchivo;
            Secuencias.Clear();
            Pares.Clear();
            Candidatas.Clear();
            Grupos.Clear();
            ActualizarVista();

            await Contexto.Trabajar(L.T("Leyendo el programa exportado…"), async () =>
            {
                Cargando = true;
                try
                {
                    _archivo = ArchivoSincro.Cargar(rutaArchivo);
                    if (!string.IsNullOrWhiteSpace(idiomaEdicion) && idiomaEdicion != _archivo.IdiomaEdicion)
                    {
                        _archivo.IdiomaEdicion = idiomaEdicion;
                        _archivo.Guardar(rutaArchivo);
                    }
                    string idioma = _archivo.IdiomaEdicion;
                    var programa = await Task.Run(() => PlcProgram.Load(carpeta, idioma));
                    _programa = programa;
                    await Analizar(null, null);
                    Contexto.PonerFallos(programa.Warnings);
                    Contexto.MostrarEstado(Resumen, error: ResumenConPares);
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    _programa = null;
                    Contexto.MostrarEstado(L.T("No se pudo leer la exportación: {0}", e.Message), error: true);
                }
                finally
                {
                    Cargando = false;
                    ActualizarVista();
                }
            });
        }

        // Calcula los pares de todas las secuencias con las decisiones guardadas y vuelve a donde estaba.
        private async Task Analizar(int? secuencia, string par)
        {
            var programa = _programa;
            var grupos = _archivo.Grupos();
            string idioma = L.Idioma == L.Ingles ? "english" : "espanol";
            var filas = await Task.Run(() =>
            {
                var checker = programa.SyncChecker(grupos);
                return programa.Sequences
                    .Select(s => new FilaSecuencia(s, Zona(programa, s),
                        checker.Check(s).Select(c => new FilaPar(c, Paso(c.StepA, idioma), Paso(c.StepB, idioma))).ToList()))
                    .Where(f => f.Pares.Count > 0)
                    .ToList();
            });

            Secuencias.Clear();
            foreach (var fila in filas)
                Secuencias.Add(fila);
            Grupos.Clear();
            foreach (var grupo in grupos)
                Grupos.Add(new FilaGrupo(grupo));

            int total = filas.Sum(f => f.Pares.Count);
            ResumenConPares = total > 0;
            Resumen = total == 0
                ? L.T("{0} secuencias: ningún par de pasos puede sincronizar a la vez", programa.Sequences.Count)
                : L.T("{0} pares posibles en {1} de {2} secuencias", total, filas.Count, programa.Sequences.Count);

            SecuenciaSeleccionada = filas.FirstOrDefault(f => f.Secuencia.Sequence == secuencia) ?? filas.FirstOrDefault();
            ParSeleccionado = Pares.FirstOrDefault(p => p.Par == par) ?? Pares.FirstOrDefault();
            ActualizarVista();
        }

        private static string Zona(PlcProgram programa, SequenceBlock secuencia) =>
            programa.OperationModeOf(secuencia.Sequence) is int om ? $"OM{om}" : "*";

        private static string Paso(SequenceStep paso, string idioma) => $"{paso.Label}  {paso.Title(idioma)}";

        partial void OnSecuenciaSeleccionadaChanged(FilaSecuencia value)
        {
            Pares.Clear();
            foreach (var par in value?.Pares ?? (IReadOnlyList<FilaPar>)Array.Empty<FilaPar>())
                Pares.Add(par);
            ParSeleccionado = Pares.FirstOrDefault();
        }

        partial void OnParSeleccionadoChanged(FilaPar value)
        {
            Candidatas.Clear();
            if (value != null && SecuenciaSeleccionada != null && _programa != null)
            {
                var checker = _programa.SyncChecker(_archivo.Grupos());
                foreach (string senal in checker.ExclusionCandidates(value.Conflicto, SecuenciaSeleccionada.Secuencia.Permanent))
                {
                    var candidata = new FilaCandidata(senal, Comentario(senal));
                    candidata.PropertyChanged += (_, __) => MarcarExcluyentesCommand.NotifyCanExecuteChanged();
                    Candidatas.Add(candidata);
                }
            }
            MarcarExcluyentesCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HayCandidatas));
            OnPropertyChanged(nameof(SinCandidatas));
        }

        // Comentario de una señal simple para ayudar a decidir; las comparaciones no tienen.
        private string Comentario(string senal)
        {
            var info = _programa.Symbols.Find(new Operand(OperandScope.Global, TextoSenal.Componentes(senal), senal));
            return info?.Comment.Get(_archivo.IdiomaEdicion) ?? "";
        }

        // Al menos dos señales para que la decisión diga algo.
        private bool PuedeMarcar() => Libre && Candidatas.Count(c => c.Marcada) >= 2;

        [RelayCommand(CanExecute = nameof(PuedeMarcar))]
        private async Task MarcarExcluyentes()
        {
            var par = ParSeleccionado;
            var fila = SecuenciaSeleccionada;
            if (par == null || fila == null)
                return;

            var grupo = new ExclusiveGroup(Candidatas.Where(c => c.Marcada).Select(c => c.Senal).ToList())
            {
                Note = L.T("Secuencia {0}: {1}", fila.Secuencia.Sequence, par.Par),
            };
            int posicion = Pares.IndexOf(par);
            if (!GuardarGrupos(_archivo.Grupos().Concat(new[] { grupo })))
                return;
            await Analizar(fila.Secuencia.Sequence, null);
            // Se sigue en el par que ha pasado a ocupar ese sitio, para revisar la lista de corrido.
            ParSeleccionado = Pares.ElementAtOrDefault(Math.Min(posicion, Pares.Count - 1));
        }

        [RelayCommand]
        private async Task BorrarGrupo(FilaGrupo fila)
        {
            if (fila == null)
                return;
            if (!GuardarGrupos(_archivo.Grupos().Where(g => !g.Equals(fila.Grupo))))
                return;
            await Analizar(SecuenciaSeleccionada?.Secuencia.Sequence, ParSeleccionado?.Par);
        }

        private bool GuardarGrupos(IEnumerable<ExclusiveGroup> grupos)
        {
            var anteriores = _archivo.Grupos();
            _archivo.PonerGrupos(grupos.ToList());
            try
            {
                _archivo.Guardar(_rutaArchivo);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _archivo.PonerGrupos(anteriores);
                Contexto.MostrarEstado(L.T("No se pudo guardar la decisión: {0}", e.Message), error: true);
                return false;
            }
        }

        private void ActualizarVista()
        {
            OnPropertyChanged(nameof(HayPrograma));
            OnPropertyChanged(nameof(HayPares));
            OnPropertyChanged(nameof(HayGrupos));
            OnPropertyChanged(nameof(TituloGrupos));
        }

        protected override void ActualizarComandos()
        {
            AbrirCarpetaCommand.NotifyCanExecuteChanged();
            ExportarDesdeTiaCommand.NotifyCanExecuteChanged();
            MarcarExcluyentesCommand.NotifyCanExecuteChanged();
        }
    }
}
