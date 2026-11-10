using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    public partial class NumeradorViewModel : Herramienta
    {
        private static readonly string[] Bloques =
        {
            "GeneralMessages_FB", "OM1_Messages_FB", "OM2_Messages_FB", "OM3_Messages_FB",
            "OM4_Messages_FB", "OM5_Messages_FB", "OM6_Messages_FB", "OM7_Messages_FB",
            "OM8_Messages_FB"
        };

        private static readonly Regex NumeroMensaje = new Regex(@"(<Component[^>]*Name=\""no_)\d+(?=\"")", RegexOptions.IgnoreCase);

        public NumeradorViewModel(Contexto contexto) : base(contexto)
        {
        }

        public override string Nombre => L.T("Numerador");
        public override string Descripcion => L.T("Renumera de 1 en adelante los mensajes de todos los FB de mensajes del proyecto (GeneralMessages_FB y OM1…OM8_Messages_FB). Es el paso previo a generar el DiagAddon.");
        public override string Icono => "hash";
        public override bool UsaTia => true;

        public string ListaBloques => string.Join(", ", Bloques);

        [RelayCommand(CanExecute = nameof(PuedeUsarTia))]
        private async Task Numerar()
        {
            await Contexto.Trabajar(L.T("Compilando el proyecto…"), async () =>
            {
                var progreso = ProgresoEnEstado();
                // Compila, y en cada PLC cada FB de mensajes se exporta, se renumera y se vuelve a importar en su sitio.
                // Los que el proyecto no tiene (estaciones que no existen) no salen al buscar.
                var resultado = await Contexto.Tia().EnProyecto(p =>
                {
                    p.CompilarTodo();
                    var bloques = Bloques.SelectMany(p.BuscarPorNombre).ToList();
                    return p.ModificarBloques(bloques, (_, xml) => Renumerar(xml), compilar: false, progreso);
                });

                int hechos = resultado.Correctos;
                var fallos = resultado.Fallos;
                Contexto.PonerFallos(fallos);
                Contexto.MostrarEstado(hechos == 0 && fallos.Count == 0
                    ? L.T("El proyecto no tiene ningún FB de mensajes de SICAR.")
                    : fallos.Count == 0
                        ? L.T("{0} FB de mensajes renumerado(s). Guarda el proyecto en TIA para conservarlos.", hechos)
                        : L.T("{0} FB de mensajes renumerado(s), {1} con error (ver abajo).", hechos, fallos.Count),
                    error: fallos.Count > 0 || hechos == 0);
            });
        }

        /// <summary>Numera los mensajes del FB de 1 en adelante, en el orden en que aparecen.</summary>
        internal static string Renumerar(string xml)
        {
            int contador = 0;
            return NumeroMensaje.Replace(xml, m => m.Groups[1].Value + ++contador);
        }

        protected override void ActualizarComandos() => NumerarCommand.NotifyCanExecuteChanged();
    }
}
