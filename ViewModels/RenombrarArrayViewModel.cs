using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
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
            string bloque = NombreBloque.Trim();
            string array = NombreArray.Trim();
            await Contexto.Trabajar(L.T("Exportando {0}…", bloque), async () =>
            {
                int cambios = await Contexto.Tia().EnProyecto(p =>
                {
                    string temporal = Path.Combine(Path.GetTempPath(), "HerramientasSICAR", "RenumerarArray");
                    try
                    {
                        p.CompilarBloque(bloque);
                        var grupo = p.GrupoDe(bloque);
                        string xml = p.ExportarBloque(bloque, temporal);
                        int n = RenumerarIndices(xml, array);
                        // Se importa encima del original, en su mismo grupo.
                        p.ImportarBloque(xml, grupo);
                        return n;
                    }
                    finally
                    {
                        if (Directory.Exists(temporal))
                            Directory.Delete(temporal, true);
                    }
                });
                Contexto.MostrarEstado(L.T("{0}: {1} uso(s) de {2} renumerados. Guarda el proyecto en TIA para conservarlos.", bloque, cambios, array));
            });
        }

        /// <summary>Cambia el índice de cada uso del array por 1, 2, 3… en el orden en que aparecen en el bloque.</summary>
        private static int RenumerarIndices(string rutaXml, string nombreArray)
        {
            string patron = $@"(<Component Name=""{Regex.Escape(nombreArray)}"" AccessModifier=""Array"">.*?<ConstantValue>)(\d+)(</ConstantValue>)";
            var regex = new Regex(patron, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            string contenido = File.ReadAllText(rutaXml);
            int contador = 0;
            string resultado = regex.Replace(contenido, m => m.Groups[1].Value + ++contador + m.Groups[3].Value);
            if (contador == 0)
                throw new InvalidOperationException(L.T("No se encontró el array '{0}' en el bloque.", nombreArray));

            File.WriteAllText(rutaXml, resultado);
            return contador;
        }

        private bool PuedeRenumerar() => PuedeUsarTia && !string.IsNullOrWhiteSpace(NombreBloque) && !string.IsNullOrWhiteSpace(NombreArray);

        protected override void ActualizarComandos() => RenumerarCommand.NotifyCanExecuteChanged();
    }
}
