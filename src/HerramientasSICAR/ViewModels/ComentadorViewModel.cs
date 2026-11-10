using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using HerramientasSICAR.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
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
            // Los _FB de la carpeta de secuencias, plegables por OM; salen marcados los de secuencia de las estaciones.
            Selector = new SelectorBloques(contexto, VistaSelector.Secuencias,
                (_, b) => ComentariosSecuencia.EsFb(b.Nombre) && !b.Protegido,
                conCasillas: true, marcadoInicial: ComentariosSecuencia.MarcadoPorDefecto);
            Selector.Cambiado += () =>
            {
                OnPropertyChanged(nameof(Resumen));
                ComentarCommand.NotifyCanExecuteChanged();
            };
        }

        public override string Nombre => L.T("Comentador");
        public override string Descripcion => L.T("Escribe en cada segmento de los FB de secuencia un comentario en inglés, alemán y castellano a partir de su título, directamente en el proyecto de TIA.");
        public override string Icono => "languages";
        public override bool UsaTia => true;

        public SelectorBloques Selector { get; }

        public IReadOnlyList<string> Idiomas { get; } = new[] { L.T("Castellano"), L.T("Inglés"), L.T("Alemán") };

        [ObservableProperty]
        private int _idiomaIndice;

        [ObservableProperty]
        private bool _traducir = true;

        public string Resumen => L.T("{0} FB marcados", Selector.NumeroMarcados);

        private IdiomaTitulos Idioma => (IdiomaTitulos)IdiomaIndice;

        partial void OnIdiomaIndiceChanged(int value)
        {
            Contexto.Ajustes.IdiomaTitulosComentador = value;
            Contexto.Ajustes.Guardar();
        }

        [RelayCommand(CanExecute = nameof(PuedeComentar))]
        private async Task Comentar()
        {
            var fbs = Selector.Marcados();
            if (!await Contexto.Dialogos.Confirmar(L.T("Comentar"),
                    L.T("Se exportan {0} FB, se reescribe el comentario de cada segmento y se importan en TIA sobrescribiéndolos. Guarda antes el proyecto y cierra en TIA los FB que vayas a comentar. ¿Seguir?", fbs.Count),
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

        private bool PuedeComentar() => PuedeUsarTia && Selector.NumeroMarcados > 0;

        protected override void ActualizarComandos()
        {
            ComentarCommand.NotifyCanExecuteChanged();
        }
    }
}
