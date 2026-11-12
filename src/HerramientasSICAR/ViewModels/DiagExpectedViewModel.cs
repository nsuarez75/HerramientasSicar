using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    public partial class DiagExpectedViewModel : Herramienta
    {
        public DiagExpectedViewModel(Contexto contexto) : base(contexto)
        {
            // Solo los FB de secuencia de las estaciones, plegables por OM.
            Selector = new SelectorBloques(contexto, VistaSelector.Secuencias,
                (grupos, b) => b.Type == "FB" && !b.IsProtected && ComentariosSecuencia.MarcadoPorDefecto(grupos, b.Name));
            Selector.Cambiado += () =>
            {
                OnPropertyChanged(nameof(NombreBloque));
                GenerarCommand.NotifyCanExecuteChanged();
            };
        }

        public override string Nombre => "DiagExpected";
        public override string Descripcion => L.T("Dado un FB de secuencia, genera un Excel con el estado que deben tener actuadores y sensores en cada paso. Es una primera plantilla: revísala antes de usarla.");
        public override string Icono => "file-sheet";
        public override bool UsaTia => true;

        public SelectorBloques Selector { get; }

        public string NombreBloque => Selector.BloqueSeleccionado?.Name ?? L.T("Elige en el árbol el FB de secuencia.");

        [RelayCommand(CanExecute = nameof(PuedeGenerar))]
        private async Task Generar()
        {
            var bloque = Selector.BloqueSeleccionado;
            await Contexto.Trabajar(L.T("Exportando {0}…", bloque.Name), async () =>
            {
                // Exportar compila el bloque si hace falta. El XML va a una carpeta temporal que se borra al terminar: en la
                // carpeta del proyecto solo queda el Excel.
                var tia = await Contexto.TiaAsync();
                using (var temporal = new TempFolder())
                {
                    var exportacion = await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = { bloque }, Folder = temporal.Path, Layout = ExportLayout.PerBlock });
                    if (exportacion.Files.Count == 0)
                    {
                        Contexto.MostrarResultado(exportacion, L.T("No se ha podido exportar {0}.", bloque.Name));
                        return;
                    }
                    string xml = exportacion.Files.First().Path;
                    string excel = Path.Combine(Contexto.Proyecto.ProjectFolder, bloque.Name + "_DIAG.xlsx");
                    await Task.Run(() => new TIADiagExpected().ProcesarYGenerarExcel(xml, excel));
                    Contexto.MostrarEstado(L.T("Excel generado: {0}", excel));
                }
            });
        }

        private bool PuedeGenerar() => PuedeUsarTia && Selector.BloqueSeleccionado != null;

        protected override void ActualizarComandos() => GenerarCommand.NotifyCanExecuteChanged();
    }
}
