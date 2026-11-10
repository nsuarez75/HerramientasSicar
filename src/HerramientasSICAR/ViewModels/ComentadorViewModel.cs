using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Un FB de la carpeta de secuencias en la lista del Comentador.</summary>
    public sealed partial class FilaFbComentador : ObservableObject
    {
        private readonly Action _alCambiar;

        public FilaFbComentador(FbSecuencia fb, Action alCambiar)
        {
            Fb = fb;
            _alCambiar = alCambiar;
            _marcado = !fb.Protegido && ComentariosSecuencia.MarcadoPorDefecto(fb.Grupos, fb.Nombre);
        }

        public FbSecuencia Fb { get; }
        public string Nombre => Fb.Nombre;

        /// <summary>Carpetas por debajo de la de secuencias, para situar el bloque.</summary>
        public string Carpeta
        {
            get
            {
                int i = Fb.Grupos.ToList().IndexOf(ComentariosSecuencia.CarpetaSecuencias);
                return string.Join(" › ", Fb.Grupos.Skip(i + 1));
            }
        }

        public bool Protegido => Fb.Protegido;
        public bool SePuedeMarcar => !Fb.Protegido;

        [ObservableProperty]
        private bool _marcado;

        partial void OnMarcadoChanged(bool value) => _alCambiar();
    }

    /// <summary>
    /// Comentador: escribe en cada segmento de los FB de secuencia elegidos un comentario de tres líneas
    /// (inglés, alemán y castellano) a partir de su título. Exporta los FB a una carpeta temporal, cambia el XML,
    /// los importa en TIA sobrescribiéndolos y borra la carpeta.
    /// </summary>
    public partial class ComentadorViewModel : Herramienta
    {
        public ComentadorViewModel(Contexto contexto) : base(contexto)
        {
            _idiomaIndice = Math.Max(0, Math.Min(2, contexto.Ajustes.IdiomaTitulosComentador));
            Contexto.PropertyChanged += AlCambiarProyecto;
        }

        public override string Nombre => L.T("Comentador");
        public override string Descripcion => L.T("Escribe en cada segmento de los FB de secuencia un comentario en inglés, alemán y castellano a partir de su título, directamente en el proyecto de TIA.");
        public override string Icono => "languages";
        public override bool UsaTia => true;

        public ObservableCollection<FilaFbComentador> Fbs { get; } = new ObservableCollection<FilaFbComentador>();

        public IReadOnlyList<string> Idiomas { get; } = new[] { L.T("Castellano"), L.T("Inglés"), L.T("Alemán") };

        [ObservableProperty]
        private int _idiomaIndice;

        [ObservableProperty]
        private bool _traducir = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayFbs), nameof(SinFbs))]
        private string _plc;

        public bool HayFbs => Fbs.Count > 0;
        public bool SinFbs => !HayFbs;
        public int Marcados => Fbs.Count(f => f.Marcado);
        public string Ayuda => L.T("Pulsa «Buscar FB de secuencia» para ver los bloques _FB de la carpeta {0}. Salen marcados los de secuencia de cada estación (OM1 a OM8), sin Summary, Messages ni 50_Safety.", ComentariosSecuencia.CarpetaSecuencias);
        public string Resumen => L.T("{0} de {1} FB marcados", Marcados, Fbs.Count);

        private IdiomaTitulos Idioma => (IdiomaTitulos)IdiomaIndice;

        partial void OnIdiomaIndiceChanged(int value)
        {
            Contexto.Ajustes.IdiomaTitulosComentador = value;
            Contexto.Ajustes.Guardar();
        }

        // Si se conecta otro proyecto, la lista ya no vale.
        private void AlCambiarProyecto(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Contexto.ProyectoConectado))
                PonerFbs(null, new List<FbSecuencia>());
        }

        private void PonerFbs(string plc, IEnumerable<FbSecuencia> fbs)
        {
            Fbs.Clear();
            foreach (var fb in fbs)
                Fbs.Add(new FilaFbComentador(fb, AlCambiarMarcas));
            Plc = plc;
            OnPropertyChanged(nameof(HayFbs));
            OnPropertyChanged(nameof(SinFbs));
            AlCambiarMarcas();
        }

        private void AlCambiarMarcas()
        {
            OnPropertyChanged(nameof(Marcados));
            OnPropertyChanged(nameof(Resumen));
            ComentarCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(PuedeUsarTia))]
        private async Task BuscarFbs()
        {
            List<string> plcs = null;
            await Contexto.Trabajar(L.T("Buscando los PLC del proyecto…"), async () =>
            {
                plcs = await Contexto.Tia().EnProyecto(p => p.NombresPlc());
            });
            if (plcs == null)
                return;
            if (plcs.Count == 0)
            {
                Contexto.MostrarEstado(L.T("No se encontró ningún PLC con software en el proyecto."), error: true);
                return;
            }
            string plc = plcs[0];
            if (plcs.Count > 1)
            {
                var elegidos = await Contexto.Dialogos.ElegirVarios(L.T("Buscar FB de secuencia"), L.T("¿De qué PLC? Se usa el primero que marques."),
                    plcs, plcs.Select((_, i) => i == 0).ToList(), L.T("Buscar"), L.T("Cancelar"));
                if (elegidos == null || elegidos.Count == 0)
                    return;
                plc = plcs[elegidos[0]];
            }

            await Contexto.Trabajar(L.T("Buscando los FB de {0}…", plc), async () =>
            {
                var fbs = await Contexto.Tia().EnProyecto(p => p.FbsDeSecuencias(plc));
                PonerFbs(plc, fbs);
                Contexto.MostrarEstado(L.T("{0}: {1} FB en {2}. Marcados los de secuencia.", plc, fbs.Count, ComentariosSecuencia.CarpetaSecuencias));
            });
        }

        [RelayCommand]
        private void MarcarTodos()
        {
            foreach (var fila in Fbs.Where(f => f.SePuedeMarcar))
                fila.Marcado = true;
        }

        [RelayCommand]
        private void MarcarNinguno()
        {
            foreach (var fila in Fbs)
                fila.Marcado = false;
        }

        [RelayCommand(CanExecute = nameof(PuedeComentar))]
        private async Task Comentar()
        {
            var fbs = Fbs.Where(f => f.Marcado).Select(f => f.Fb).ToList();
            string plc = Plc;
            if (!await Contexto.Dialogos.Confirmar(L.T("Comentar"),
                    L.T("Se exportan {0} FB de {1}, se reescribe el comentario de cada segmento y se importan en TIA sobrescribiéndolos. Guarda antes el proyecto y cierra en TIA los FB que vayas a comentar. ¿Seguir?", fbs.Count, plc),
                    L.T("Comentar")))
                return;

            var idioma = Idioma;
            bool traducir = Traducir;
            string carpeta = Path.Combine(Path.GetTempPath(), "HerramientasSICAR", "Comentador", Guid.NewGuid().ToString("N"));
            try
            {
                await Contexto.Trabajar(L.T("Exportando los FB…"), async () =>
                {
                    var fallos = new List<string>();
                    var progreso = new Progress<string>(texto => Contexto.MostrarEstado(texto));

                    // 1. Exportar (en el hilo de Openness).
                    var exportados = await Contexto.Tia().EnProyecto(p =>
                    {
                        var rutas = new List<(FbSecuencia Fb, string Ruta)>();
                        for (int i = 0; i < fbs.Count; i++)
                        {
                            ((IProgress<string>)progreso).Report(L.T("Exportando {0}/{1}: {2}", i + 1, fbs.Count, fbs[i].Nombre));
                            try
                            {
                                rutas.Add((fbs[i], p.ExportarFb(plc, fbs[i], Path.Combine(carpeta, i.ToString()))));
                            }
                            catch (Exception e)
                            {
                                fallos.Add($"{fbs[i].Nombre}: {e.Message}");
                            }
                        }
                        return rutas;
                    });

                    // 2. Traducir los títulos distintos y reescribir los comentarios.
                    var xmls = await Task.Run(() => exportados.Select(e => File.ReadAllText(e.Ruta)).ToList());
                    var titulos = xmls.SelectMany(ComentariosSecuencia.Titulos).Distinct().ToList();
                    var textos = await TextosAsync(titulos, idioma, traducir, progreso);

                    int segmentos = 0;
                    await Task.Run(() =>
                    {
                        for (int i = 0; i < exportados.Count; i++)
                        {
                            var (xml, comentados) = ComentariosSecuencia.Comentar(xmls[i], t => textos[t]);
                            File.WriteAllText(exportados[i].Ruta, xml, new UTF8Encoding(true));
                            segmentos += comentados;
                        }
                    });

                    // 3. Importar en TIA, sobrescribiendo, y compilar.
                    int importados = await Contexto.Tia().EnProyecto(p =>
                    {
                        int n = 0;
                        for (int i = 0; i < exportados.Count; i++)
                        {
                            var fb = exportados[i].Fb;
                            ((IProgress<string>)progreso).Report(L.T("Importando {0}/{1}: {2}", i + 1, exportados.Count, fb.Nombre));
                            try
                            {
                                if (!p.ImportarFb(plc, fb, exportados[i].Ruta))
                                    fallos.Add(L.T("{0}: importado, pero no compila; revísalo en TIA", fb.Nombre));
                                n++;
                            }
                            catch (Exception e)
                            {
                                fallos.Add($"{fb.Nombre}: {e.Message}");
                            }
                        }
                        return n;
                    });

                    Contexto.PonerFallos(fallos);
                    Contexto.MostrarEstado(fallos.Count == 0
                        ? L.T("Comentados {0} segmentos en {1} FB.", segmentos, importados)
                        : L.T("Comentados {0} segmentos en {1} FB. Hay {2} aviso(s) (ver abajo).", segmentos, importados, fallos.Count),
                        error: fallos.Count > 0);
                });
            }
            finally
            {
                BorrarCarpeta(carpeta);
            }
        }

        /// <summary>Las tres líneas de cada título: la de su idioma tal cual y las otras dos traducidas (o iguales).</summary>
        private async Task<Dictionary<string, TextosComentario>> TextosAsync(IReadOnlyList<string> titulos, IdiomaTitulos idioma,
            bool traducir, IProgress<string> progreso)
        {
            string origen = ComentariosSecuencia.Codigo(idioma);
            var destinos = new[] { "en", "de", "es" }.Where(d => d != origen).ToList();
            var traducciones = new ConcurrentDictionary<(string, string), string>();

            if (traducir && titulos.Count > 0)
            {
                int total = titulos.Count * destinos.Count;
                int hechas = 0;
                using (var semaforo = new SemaphoreSlim(Traductor.MaxConcurrentes))
                {
                    await Task.WhenAll(titulos.SelectMany(t => destinos.Select(d => (t, d))).Select(async par =>
                    {
                        await semaforo.WaitAsync();
                        try
                        {
                            traducciones[par] = await Traductor.TraducirAsync(par.t, par.d, origen);
                        }
                        finally
                        {
                            semaforo.Release();
                            progreso.Report(L.T("Traduciendo títulos: {0}/{1}", Interlocked.Increment(ref hechas), total));
                        }
                    }));
                }
            }

            string En(string t, string codigo) =>
                codigo == origen ? t : traducciones.TryGetValue((t, codigo), out var traducido) ? traducido : t;
            return titulos.ToDictionary(t => t, t => new TextosComentario(En(t, "en"), En(t, "de"), En(t, "es")));
        }

        private static void BorrarCarpeta(string carpeta)
        {
            try
            {
                if (Directory.Exists(carpeta))
                    Directory.Delete(carpeta, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Es una carpeta temporal: si Windows la tiene bloqueada, se queda en %TEMP%.
            }
        }

        private bool PuedeComentar() => PuedeUsarTia && Plc != null && Fbs.Any(f => f.Marcado);

        protected override void ActualizarComandos()
        {
            BuscarFbsCommand.NotifyCanExecuteChanged();
            ComentarCommand.NotifyCanExecuteChanged();
        }
    }
}
