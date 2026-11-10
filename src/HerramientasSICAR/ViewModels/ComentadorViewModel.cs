using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Un FB de la carpeta de secuencias en la lista del Comentador.</summary>
    public sealed partial class FilaFbComentador : ObservableObject
    {
        private readonly Action _alCambiar;

        public FilaFbComentador(BloqueRef fb, Action alCambiar)
        {
            Fb = fb;
            _alCambiar = alCambiar;
            _marcado = !fb.Protegido && ComentariosSecuencia.MarcadoPorDefecto(fb.Grupos, fb.Nombre);
        }

        public BloqueRef Fb { get; }
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
                PonerFbs(null, new List<BloqueRef>());
        }

        private void PonerFbs(string plc, IEnumerable<BloqueRef> fbs)
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
            string plc = await ElegirPlc(L.T("Buscar FB de secuencia"), L.T("Buscar"));
            if (plc == null)
                return;

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
            await Contexto.Trabajar(L.T("Exportando los FB…"), async () =>
            {
                var progreso = ProgresoEnEstado();
                var resultado = new ResultadoOperacion();
                int segmentos = 0;
                using (var temporal = new CarpetaTemporal())
                {
                    // 1. Exportar (en el hilo de Openness).
                    var exportados = await Contexto.Tia().EnProyecto(p => p.ExportarVarios(fbs, temporal.Ruta, progreso, resultado));

                    // 2. Traducir los títulos distintos y reescribir los comentarios. Esto va fuera del hilo de Openness
                    //    porque el traductor está en internet.
                    var xmls = await Task.Run(() => exportados.Select(e => File.ReadAllText(e.Ruta)).ToList());
                    var titulos = xmls.SelectMany(ComentariosSecuencia.Titulos).Distinct().ToList();
                    var textos = await TextosAsync(titulos, idioma, traducir, progreso);
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
                    await Contexto.Tia().EnProyecto(p => p.ImportarVarios(exportados, compilar: true, progreso, resultado));
                }

                var fallos = resultado.Fallos;
                Contexto.PonerFallos(fallos);
                Contexto.MostrarEstado(fallos.Count == 0
                    ? L.T("Comentados {0} segmentos en {1} FB.", segmentos, resultado.Correctos)
                    : L.T("Comentados {0} segmentos en {1} FB. Hay {2} aviso(s) (ver abajo).", segmentos, resultado.Correctos, fallos.Count),
                    error: fallos.Count > 0);
            });
        }

        /// <summary>Las tres líneas de cada título: la de su idioma tal cual y las otras dos traducidas (o iguales).</summary>
        private static async Task<Dictionary<string, TextosComentario>> TextosAsync(IReadOnlyList<string> titulos, IdiomaTitulos idioma,
            bool traducir, IProgress<string> progreso)
        {
            string origen = ComentariosSecuencia.Codigo(idioma);
            var destinos = new[] { "en", "de", "es" }.Where(d => d != origen).ToList();
            var traducciones = traducir
                ? await Traductor.TraducirVariosAsync(titulos.SelectMany(t => destinos.Select(d => (t, d))), origen,
                    new Progress<(int Hechas, int Total)>(a => progreso.Report(L.T("Traduciendo títulos: {0}/{1}", a.Hechas, a.Total))))
                : new Dictionary<(string Texto, string Destino), string>();

            string En(string t, string codigo) =>
                codigo == origen ? t : traducciones.TryGetValue((t, codigo), out var traducido) ? traducido : t;
            return titulos.ToDictionary(t => t, t => new TextosComentario(En(t, "en"), En(t, "de"), En(t, "es")));
        }

        private bool PuedeComentar() => PuedeUsarTia && Plc != null && Fbs.Any(f => f.Marcado);

        protected override void ActualizarComandos()
        {
            BuscarFbsCommand.NotifyCanExecuteChanged();
            ComentarCommand.NotifyCanExecuteChanged();
        }
    }
}
