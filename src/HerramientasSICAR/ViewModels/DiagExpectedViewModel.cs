using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using System.IO;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    public partial class DiagExpectedViewModel : Herramienta
    {
        public DiagExpectedViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => "DiagExpected";
        public override string Descripcion => L.T("Dado un FB de secuencia, genera un Excel con el estado que deben tener actuadores y sensores en cada paso. Es una primera plantilla: revísala antes de usarla.");
        public override string Icono => "file-sheet";
        public override bool UsaTia => true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
        private string _nombreBloque = "";

        [RelayCommand(CanExecute = nameof(PuedeGenerar))]
        private async Task Generar()
        {
            string nombre = NombreBloque.Trim();
            var bloque = await ElegirBloque(nombre, "DiagExpected");
            if (bloque == null)
                return;

            await Contexto.Trabajar(L.T("Exportando {0}…", nombre), async () =>
            {
                string excel = await Contexto.Tia().EnProyecto(p =>
                {
                    // Exportar compila el bloque si hace falta.
                    string xml = p.Exportar(bloque, p.Carpeta);
                    string ruta = Path.Combine(p.Carpeta, nombre + "_DIAG.xlsx");
                    new TIADiagExpected().ProcesarYGenerarExcel(xml, ruta);
                    return ruta;
                });
                Contexto.MostrarEstado(L.T("Excel generado: {0}", excel));
            });
        }

        private bool PuedeGenerar() => PuedeUsarTia && !string.IsNullOrWhiteSpace(NombreBloque);

        protected override void ActualizarComandos() => GenerarCommand.NotifyCanExecuteChanged();
    }
}
