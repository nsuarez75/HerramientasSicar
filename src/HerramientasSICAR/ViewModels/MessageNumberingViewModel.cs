using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaConnector.Client;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    public partial class MessageNumberingViewModel : Tool
    {
        private static readonly string[] Blocks =
        {
            "GeneralMessages_FB", "OM1_Messages_FB", "OM2_Messages_FB", "OM3_Messages_FB",
            "OM4_Messages_FB", "OM5_Messages_FB", "OM6_Messages_FB", "OM7_Messages_FB",
            "OM8_Messages_FB"
        };

        private static readonly Regex MessageNumber = new Regex(@"(<Component[^>]*Name=\""no_)\d+(?=\"")", RegexOptions.IgnoreCase);

        public MessageNumberingViewModel(ToolContext context) : base(context)
        {
        }

        public override string Name => L.T("Numerador");
        public override string Description => L.T("Renumera de 1 en adelante los mensajes de todos los FB de mensajes del proyecto (GeneralMessages_FB y OM1…OM8_Messages_FB). Es el paso previo a generar el DiagAddon.");
        public override string Icon => "hash";
        public override bool UsesTia => true;

        public string BlockList => string.Join(", ", Blocks);

        [RelayCommand(CanExecute = nameof(CanUseTia))]
        private async Task Number()
        {
            await ToolContext.Work(L.T("Compilando el proyecto…"), async () =>
            {
                var tia = await ToolContext.TiaAsync();
                var progress = ToolContext.TiaProgress();
                var plcs = ToolContext.ProjectFile.Devices.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name).ToList();
                if (plcs.Count == 0)
                {
                    ToolContext.ShowStatus(L.T("No se encontró ningún PLC con software en el proyecto."), error: true);
                    return;
                }

                // Compila, y en cada PLC cada FB de mensajes se exporta, se renumera y se vuelve a importar en su sitio.
                // Los que el proyecto no tiene (estaciones que no existen) no salen al buscar. El árbol se lee una vez
                // de TIA (no el ya leído al conectar, que puede estar viejo) y se busca en él.
                await tia.CompileAsync(new CompileArgs { Scope = CompileScope.Project }, progress);
                var tree = await tia.ReadTreeAsync(plcs, new string[0], progress);
                var blocks = tree.Plcs
                    .SelectMany(p => BlockSearch.Find(p, (_, b) => Blocks.Contains(b.Name, StringComparer.OrdinalIgnoreCase)))
                    .ToList();
                var result = await tia.ModifyBlocksAsync(blocks, (_, xml) => Task.FromResult(Renumber(xml)), compile: false, progress);

                int doneCount = result.Succeeded;
                var failures = TiaTexts.Failures(result, importing: true);
                ToolContext.SetFailures(failures);
                ToolContext.ShowStatus(doneCount == 0 && failures.Count == 0
                    ? L.T("El proyecto no tiene ningún FB de mensajes de SICAR.")
                    : failures.Count == 0
                        ? L.T("{0} FB de mensajes renumerado(s). Guarda el proyecto en TIA para conservarlos.", doneCount)
                        : L.T("{0} FB de mensajes renumerado(s), {1} con error (ver abajo).", doneCount, failures.Count),
                    error: failures.Count > 0 || doneCount == 0);
            });
        }

        /// <summary>Numera los mensajes del FB de 1 en adelante, en el orden en que aparecen.</summary>
        internal static string Renumber(string xml)
        {
            int counter = 0;
            return MessageNumber.Replace(xml, m => m.Groups[1].Value + ++counter);
        }

        protected override void UpdateCommands() => NumberCommand.NotifyCanExecuteChanged();
    }
}
