using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logic;
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
    public partial class CommentatorViewModel : Tool
    {
        public CommentatorViewModel(ToolContext context) : base(context)
        {
            _languageIndex = Math.Max(0, Math.Min(2, context.Settings.CommentatorTitleLanguage));
            // Los _FB de la carpeta de secuencias, plegables por OM; salen marcados los de secuencia de las estaciones.
            Selector = new BlockSelector(context, SelectorView.Sequences,
                (_, b) => SequenceComments.IsFb(b.Name) && !b.IsProtected,
                withCheckBoxes: true, initiallyChecked: SequenceComments.CheckedByDefault);
            Selector.Changed += () =>
            {
                OnPropertyChanged(nameof(Summary));
                CommentCommand.NotifyCanExecuteChanged();
            };
        }

        public override string Name => L.T("Comentador");
        public override string Description => L.T("Escribe en cada segmento de los FB de secuencia un comentario en inglés, alemán y castellano a partir de su título, directamente en el proyecto de TIA.");
        public override string Icon => "languages";
        public override bool UsesTia => true;

        public BlockSelector Selector { get; }

        public IReadOnlyList<string> Languages { get; } = new[] { L.T("Castellano"), L.T("Inglés"), L.T("Alemán") };

        [ObservableProperty]
        private int _languageIndex;

        [ObservableProperty]
        private bool _translate = true;

        public string Summary => L.T("{0} FB marcados", Selector.CheckedCount);

        private TitleLanguage Language => (TitleLanguage)LanguageIndex;

        partial void OnLanguageIndexChanged(int value)
        {
            ToolContext.Settings.CommentatorTitleLanguage = value;
            ToolContext.Settings.Save();
        }

        [RelayCommand(CanExecute = nameof(CanComment))]
        private async Task Comment()
        {
            var fbs = Selector.CheckedItems();
            if (!await ToolContext.Dialogs.Confirm(L.T("Comentar"),
                    L.T("Se exportan {0} FB, se reescribe el comentario de cada segmento y se importan en TIA sobrescribiéndolos. Guarda antes el proyecto y cierra en TIA los FB que vayas a comentar. ¿Seguir?", fbs.Count),
                    L.T("Comentar")))
                return;

            var language = Language;
            bool translate = Translate;
            await ToolContext.Work(L.T("Exportando los FB…"), async () =>
            {
                var tia = await ToolContext.TiaAsync();
                var tiaProgress = ToolContext.TiaProgress();
                int networks = 0;
                List<string> failures;
                int commentedFbs;
                using (var temp = new TempFolder())
                {
                    // 1. Exportar, cada FB en su subcarpeta (no chocan nombres de PLC distintos).
                    var export = await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = fbs, Folder = temp.Path, Layout = ExportLayout.PerBlock }, tiaProgress);
                    var exported = export.Files;

                    // 2. Traducir los títulos distintos de todos los FB de una vez (el traductor está en internet) y
                    //    reescribir los comentarios.
                    var xmls = await Task.Run(() => exported.Select(e => File.ReadAllText(e.Path)).ToList());
                    var titles = xmls.SelectMany(SequenceComments.Titles).Distinct().ToList();
                    var texts = await TextsAsync(titles, language, translate, StatusProgress());
                    await Task.Run(() =>
                    {
                        for (int i = 0; i < exported.Count; i++)
                        {
                            var (xml, commented) = SequenceComments.Comment(xmls[i], t => texts[t]);
                            File.WriteAllText(exported[i].Path, xml, new UTF8Encoding(true));
                            networks += commented;
                        }
                    });

                    // 3. Importar en TIA encima de cada FB, en su carpeta, y compilar (nada si TIA se cerró al exportar).
                    var import = export.Stopped ? new BatchResult() : await tia.ImportBlocksAsync(new ImportBlocksArgs
                    {
                        Files = exported.Select(e => new ImportFile { Path = e.Path, Plc = fbs[e.Index].Plc, Folders = fbs[e.Index].Folders.ToList() }).ToList(),
                        CompileAfter = true
                    }, tiaProgress);
                    commentedFbs = import.Succeeded;
                    failures = TiaTexts.Failures(export).Concat(TiaTexts.Failures(import, importing: true)).ToList();
                }

                ToolContext.SetFailures(failures);
                ToolContext.ShowStatus(failures.Count == 0
                    ? L.T("Comentados {0} segmentos en {1} FB.", networks, commentedFbs)
                    : L.T("Comentados {0} segmentos en {1} FB. Hay {2} aviso(s) (ver abajo).", networks, commentedFbs, failures.Count),
                    error: failures.Count > 0);
            });
        }

        /// <summary>Las tres líneas de cada título: la de su idioma tal cual y las otras dos traducidas (o iguales).</summary>
        private static async Task<Dictionary<string, CommentTexts>> TextsAsync(IReadOnlyList<string> titles, TitleLanguage language,
            bool translate, IProgress<string> progress)
        {
            string source = SequenceComments.Code(language);
            var targets = new[] { "en", "de", "es" }.Where(d => d != source).ToList();
            var translations = translate
                ? await Translator.TranslateManyAsync(titles.SelectMany(t => targets.Select(d => (t, d))), source,
                    new Progress<(int Done, int Total)>(a => progress.Report(L.T("Traduciendo títulos: {0}/{1}", a.Done, a.Total))))
                : new Dictionary<(string Text, string Target), string>();

            string En(string t, string code) =>
                code == source ? t : translations.TryGetValue((t, code), out var translated) ? translated : t;
            return titles.ToDictionary(t => t, t => new CommentTexts(En(t, "en"), En(t, "de"), En(t, "es")));
        }

        private bool CanComment() => CanUseTia && Selector.CheckedCount > 0;

        protected override void UpdateCommands()
        {
            CommentCommand.NotifyCanExecuteChanged();
        }
    }
}
