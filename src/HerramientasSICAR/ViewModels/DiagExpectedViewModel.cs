using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logica;
using System.IO;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    public partial class DiagExpectedViewModel : Herramienta
    {
        public DiagExpectedViewModel(Contexto contexto) : base(contexto)
        {
            // Solo los FB de secuencia de las estaciones, plegables por OM.
            Selector = new SelectorBloques(contexto, VistaSelector.Secuencias,
                (grupos, b) => b.Tipo == "FB" && !b.Protegido && ComentariosSecuencia.MarcadoPorDefecto(grupos, b.Nombre));
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

        public string NombreBloque => Selector.BloqueSeleccionado?.Nombre ?? L.T("Elige en el árbol el FB de secuencia.");

        [RelayCommand(CanExecute = nameof(PuedeGenerar))]
        private async Task Generar()
        {
            var bloque = Selector.BloqueSeleccionado;
            await Contexto.Trabajar(L.T("Exportando {0}…", bloque.Nombre), async () =>
            {
                string excel = await Contexto.Tia().EnProyecto(p =>
                {
                    // Exportar compila el bloque si hace falta.
                    string xml = p.Exportar(bloque, p.Carpeta);
                    string ruta = Path.Combine(p.Carpeta, bloque.Nombre + "_DIAG.xlsx");
                    new TIADiagExpected().ProcesarYGenerarExcel(xml, ruta);
                    return ruta;
                });
                Contexto.MostrarEstado(L.T("Excel generado: {0}", excel));
            });
        }

        private bool PuedeGenerar() => PuedeUsarTia && Selector.BloqueSeleccionado != null;

        protected override void ActualizarComandos() => GenerarCommand.NotifyCanExecuteChanged();
    }
}
