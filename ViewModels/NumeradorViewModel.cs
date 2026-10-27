using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
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
                var progreso = new Progress<string>(texto => Contexto.MostrarEstado(texto));
                var fallos = new List<string>();
                int hechos = await Contexto.Tia().EnProyecto(p => NumerarBloques(p, progreso, fallos));

                Contexto.PonerFallos(fallos);
                Contexto.MostrarEstado(hechos == 0 && fallos.Count == 0
                    ? L.T("El proyecto no tiene ningún FB de mensajes de SICAR.")
                    : fallos.Count == 0
                        ? L.T("{0} FB de mensajes renumerado(s). Guarda el proyecto en TIA para conservarlos.", hechos)
                        : L.T("{0} FB de mensajes renumerado(s), {1} con error (ver abajo).", hechos, fallos.Count),
                    error: fallos.Count > 0 || hechos == 0);
            });
        }

        // En el hilo de Openness: compila, y cada FB de mensajes se exporta, se renumera y se vuelve a importar
        // en su mismo grupo. Los que el proyecto no tiene (estaciones que no existen) se saltan.
        private static int NumerarBloques(Services.ProyectoTia p, IProgress<string> progreso, List<string> fallos)
        {
            int hechos = 0;
            string temporal = Path.Combine(Path.GetTempPath(), "HerramientasSICAR", "Numerador");
            p.CompilarTodo();
            try
            {
                foreach (string bloque in Bloques)
                {
                    var grupo = p.GrupoDe(bloque);
                    if (grupo == null)
                        continue;

                    progreso.Report(L.T("Procesando {0}…", bloque));
                    try
                    {
                        string xml = p.ExportarBloque(bloque, temporal);
                        int contador = 0;
                        string nuevo = NumeroMensaje.Replace(File.ReadAllText(xml), m => m.Groups[1].Value + ++contador);
                        File.WriteAllText(xml, nuevo);

                        p.ImportarBloque(xml, grupo);
                        hechos++;
                    }
                    catch (Exception ex)
                    {
                        // Un bloque que falla no para los demás.
                        fallos.Add($"{bloque}: {ex.Message}");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(temporal))
                    Directory.Delete(temporal, true);
            }
            return hechos;
        }

        protected override void ActualizarComandos() => NumerarCommand.NotifyCanExecuteChanged();
    }
}
