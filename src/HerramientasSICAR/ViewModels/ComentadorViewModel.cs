using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TiaConnector.Contracts;

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
                (_, b) => ComentariosSecuencia.EsFb(b.Name) && !b.IsProtected,
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
                var tia = await Contexto.TiaAsync();
                var progresoTia = Contexto.ProgresoTia();
                int segmentos = 0;
                List<string> fallos;
                int comentadosFb;
                using (var temporal = new TempFolder())
                {
                    // 1. Exportar, cada FB en su subcarpeta (no chocan nombres de PLC distintos).
                    var exportacion = await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = fbs, Folder = temporal.Path, Layout = ExportLayout.PerBlock }, progresoTia);
                    var exportados = exportacion.Files;

                    // 2. Traducir los títulos distintos de todos los FB de una vez (el traductor está en internet) y
                    //    reescribir los comentarios.
                    var xmls = await Task.Run(() => exportados.Select(e => File.ReadAllText(e.Path)).ToList());
                    var titulos = xmls.SelectMany(ComentariosSecuencia.Titulos).Distinct().ToList();
                    var textos = await TextosAsync(titulos, idioma, traducir, ProgresoEnEstado());
                    await Task.Run(() =>
                    {
                        for (int i = 0; i < exportados.Count; i++)
                        {
                            var (xml, comentados) = ComentariosSecuencia.Comentar(xmls[i], t => textos[t]);
                            File.WriteAllText(exportados[i].Path, xml, new UTF8Encoding(true));
                            segmentos += comentados;
                        }
                    });

                    // 3. Importar en TIA encima de cada FB, en su carpeta, y compilar (nada si TIA se cerró al exportar).
                    var importacion = exportacion.Stopped ? new BatchResult() : await tia.ImportBlocksAsync(new ImportBlocksArgs
                    {
                        Files = exportados.Select(e => new ImportFile { Path = e.Path, Plc = fbs[e.Index].Plc, Folders = fbs[e.Index].Folders.ToList() }).ToList(),
                        CompileAfter = true
                    }, progresoTia);
                    comentadosFb = importacion.Succeeded;
                    fallos = TextosTia.Fallos(exportacion).Concat(TextosTia.Fallos(importacion, importando: true)).ToList();
                }

                Contexto.PonerFallos(fallos);
                Contexto.MostrarEstado(fallos.Count == 0
                    ? L.T("Comentados {0} segmentos en {1} FB.", segmentos, comentadosFb)
                    : L.T("Comentados {0} segmentos en {1} FB. Hay {2} aviso(s) (ver abajo).", segmentos, comentadosFb, fallos.Count),
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
