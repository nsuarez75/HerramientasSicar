using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    public partial class RenombrarArrayViewModel : Herramienta
    {
        public RenombrarArrayViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => L.T("Renumerar Array");
        public override string Descripcion => L.T("Renumera de menor a mayor todos los usos de un array dentro de un FB o FC. Útil, por ejemplo, para intercalar un segmento nuevo en un array de flancos sin reordenarlo a mano. No es exclusivo de SICAR.");
        public override string Icono => "list-ordered";
        public override bool UsaTia => true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenumerarCommand))]
        private string _nombreBloque = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenumerarCommand))]
        private string _nombreArray = "";

        [RelayCommand(CanExecute = nameof(PuedeRenumerar))]
        private async Task Renumerar()
        {
            string nombre = NombreBloque.Trim();
            string array = NombreArray.Trim();
            var bloque = await ElegirBloque(nombre, L.T("Renumerar Array"));
            if (bloque == null)
                return;

            await Contexto.Trabajar(L.T("Exportando {0}…", nombre), async () =>
            {
                int cambios = 0;
                // Se exporta, se renumera y se importa encima del original, en su mismo grupo.
                var resultado = await Contexto.Tia().EnProyecto(p =>
                    p.ModificarBloques(new[] { bloque }, (_, xml) => RenumerarIndices(xml, array, out cambios), compilar: false, ProgresoEnEstado()));
                if (resultado.Fallos.Count > 0)
                {
                    Contexto.PonerFallos(resultado.Fallos);
                    Contexto.MostrarEstado(resultado.Fallos[0], error: true);
                    return;
                }
                Contexto.MostrarEstado(L.T("{0}: {1} uso(s) de {2} renumerados. Guarda el proyecto en TIA para conservarlos.", nombre, cambios, array));
            });
        }

        /// <summary>Cambia el índice de cada uso del array por 1, 2, 3… en el orden en que aparecen en el bloque.</summary>
        internal static string RenumerarIndices(string xml, string nombreArray, out int cambios)
        {
            string patron = $@"(<Component Name=""{Regex.Escape(nombreArray)}"" AccessModifier=""Array"">.*?<ConstantValue>)(\d+)(</ConstantValue>)";
            var regex = new Regex(patron, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            int contador = 0;
            string resultado = regex.Replace(xml, m => m.Groups[1].Value + ++contador + m.Groups[3].Value);
            if (contador == 0)
                throw new InvalidOperationException(L.T("No se encontró el array '{0}' en el bloque.", nombreArray));
            cambios = contador;
            return resultado;
        }

        private bool PuedeRenumerar() => PuedeUsarTia && !string.IsNullOrWhiteSpace(NombreBloque) && !string.IsNullOrWhiteSpace(NombreArray);

        protected override void ActualizarComandos() => RenumerarCommand.NotifyCanExecuteChanged();
    }
}
