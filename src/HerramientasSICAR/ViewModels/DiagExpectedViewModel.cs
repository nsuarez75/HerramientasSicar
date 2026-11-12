using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    public partial class DiagExpectedViewModel : Tool
    {
        public DiagExpectedViewModel(ToolContext context) : base(context)
        {
            // Solo los FB de secuencia de las estaciones, plegables por OM.
            Selector = new BlockSelector(context, SelectorView.Sequences,
                (groups, b) => b.Type == "FB" && !b.IsProtected && SequenceComments.CheckedByDefault(groups, b.Name));
            Selector.Changed += () =>
            {
                OnPropertyChanged(nameof(BlockName));
                GenerateCommand.NotifyCanExecuteChanged();
            };
        }

        public override string Name => "DiagExpected";
        public override string Description => L.T("Dado un FB de secuencia, genera un Excel con el estado que deben tener actuadores y sensores en cada paso. Es una primera plantilla: revísala antes de usarla.");
        public override string Icon => "file-sheet";
        public override bool UsesTia => true;

        public BlockSelector Selector { get; }

        public string BlockName => Selector.SelectedBlock?.Name ?? L.T("Elige en el árbol el FB de secuencia.");

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private async Task Generate()
        {
            var block = Selector.SelectedBlock;
            await ToolContext.Work(L.T("Exportando {0}…", block.Name), async () =>
            {
                // Exportar compila el bloque si hace falta. El XML va a una carpeta temporal que se borra al terminar: en la
                // carpeta del proyecto solo queda el Excel.
                var tia = await ToolContext.TiaAsync();
                using (var temp = new TempFolder())
                {
                    var export = await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = { block }, Folder = temp.Path, Layout = ExportLayout.PerBlock });
                    if (export.Files.Count == 0)
                    {
                        ToolContext.ShowResult(export, L.T("No se ha podido exportar {0}.", block.Name));
                        return;
                    }
                    string xml = export.Files.First().Path;
                    string excel = Path.Combine(ToolContext.ProjectFile.ProjectFolder, block.Name + "_DIAG.xlsx");
                    await Task.Run(() => new DiagExpectedGenerator().ProcessAndGenerateExcel(xml, excel));
                    ToolContext.ShowStatus(L.T("Excel generado: {0}", excel));
                }
            });
        }

        private bool CanGenerate() => CanUseTia && Selector.SelectedBlock != null;

        protected override void UpdateCommands() => GenerateCommand.NotifyCanExecuteChanged();
    }
}
