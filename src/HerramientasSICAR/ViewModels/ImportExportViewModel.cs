using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>
    /// Saca bloques y elementos de HMI del proyecto conectado a XML y mete XML de vuelta (en el mismo proyecto o en otro).
    /// </summary>
    public partial class ImportExportViewModel : Tool
    {
        public ImportExportViewModel(ToolContext context) : base(context)
        {
            ToolContext.TreeChanged += OnTreeChanged;
        }

        // El árbol es común a todas las herramientas: se rehace al conectar y se actualiza el PLC que se vuelve a leer.
        private void OnTreeChanged(TreeChange change)
        {
            if (change.IsFull)
            {
                ShowTree(change.Devices, change.Tree);
                return;
            }
            var root = Devices.FirstOrDefault(d => d.Kind == NodeKind.Plc && d.Name == change.Plc.Name);
            if (root == null)
                return;
            root.ReplaceContent(change.Plc.Root);
            TreeReread(SelectedNode != null && SelectedNode.Root == root ? SelectedNode : root);
        }

        public override string Name => L.T("Importar / Exportar");
        public override string Description => L.T("Exporta bloques y elementos de HMI del proyecto a XML e importa XML en el proyecto.");
        public override string Icon => "replace";
        public override bool UsesTia => true;

        /// <summary>PLC y HMI del proyecto conectado, cada uno con su árbol.</summary>
        public ObservableCollection<TreeNode> Devices { get; } = new ObservableCollection<TreeNode>();

        [ObservableProperty]
        private string _filter;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Target), nameof(HasTarget), nameof(ReadText))]
        [NotifyCanExecuteChangedFor(nameof(ImportFilesCommand), nameof(ImportFolderCommand), nameof(ReadCommand))]
        private TreeNode _selectedNode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CheckedText), nameof(HasChecked))]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        private int _checkedItems;

        public bool HasChecked => CheckedItems > 0;
        public bool HasTarget => SelectedNode != null;
        public string CheckedText => CheckedItems == 0 ? L.T("Ningún elemento marcado") : L.T("{0} elemento(s) marcado(s)", CheckedItems);

        /// <summary>Botón para leer (o volver a leer) de TIA el PLC o la HMI de lo seleccionado.</summary>
        public string ReadText => SelectedNode == null ? L.T("Selecciona un PLC o una HMI para leerlo")
            : SelectedNode.Root.IsRead ? L.T("Leer de nuevo {0}", SelectedNode.Root.Name)
            : L.T("Leer {0}", SelectedNode.Root.Name);

        /// <summary>
        /// Dónde se importará: la carpeta seleccionada en el árbol (o la de un bloque seleccionado).
        /// En una HMI, cada archivo va a su rama; lo seleccionado solo cuenta para los de esa misma rama.
        /// </summary>
        public string Target
        {
            get
            {
                var node = SelectedNode;
                if (node == null)
                    return L.T("Selecciona en el árbol dónde importar");
                var parts = new List<string> { node.Root.Name };
                if (node.Branch != null)
                    parts.Add(L.T(HmiRules.FolderName(node.Branch.Value)));
                parts.AddRange(node.Groups);
                return string.Join(" › ", parts);
            }
        }

        /// <summary>
        /// PLC y HMI del proyecto recién conectado (o ninguno si se desconecta), con el árbol de los que se han leído;
        /// los demás salen «sin leer».
        /// </summary>
        private void ShowTree(IReadOnlyList<DeviceInfo> devices, ProjectTree tree)
        {
            Devices.Clear();
            SelectedNode = null;
            foreach (var device in devices)
            {
                bool isHmi = device.Kind == DeviceKind.Hmi;
                var plc = isHmi ? null : tree.Plcs.FirstOrDefault(p => p.Name == device.Name);
                var hmi = isHmi ? tree.Hmis.FirstOrDefault(h => h.Name == device.Name) : null;
                string name = DeviceNames.Visible(device, devices);
                Devices.Add(plc != null ? TreeNode.FromPlc(plc, CountChecked)
                    : hmi != null ? TreeNode.FromHmi(hmi, CountChecked, name)
                    : TreeNode.Unread(device, CountChecked, name));
            }
            OnFilterChanged(Filter);
            CountChecked();
        }

        /// <summary>Lee de TIA el PLC o la HMI de lo seleccionado: si no se leyó al conectar, o para ver cambios hechos en TIA.</summary>
        [RelayCommand(CanExecute = nameof(CanRead))]
        private async Task Read()
        {
            var target = SelectedNode;
            var device = target.Root;

            await ToolContext.Work(L.T("Leyendo el árbol del proyecto…"), async () =>
            {
                if (device.Kind == NodeKind.Hmi)
                {
                    var tia = await ToolContext.TiaAsync();
                    var tree = await tia.ReadTreeAsync(new string[0], new[] { device.Device }, ToolContext.TiaProgress());
                    device.ReplaceBranches(HmiRules.TreeOrder, tree.Hmis.Single().Branches);
                }
                else
                    await ToolContext.ReadPlcAsync(device.Device);   // lo pone en el árbol común: lo ven todas las herramientas
                TreeReread(target);
                ToolContext.ShowStatus(L.T("Árbol de {0} leído.", device.Name));
            });
        }

        partial void OnFilterChanged(string value)
        {
            foreach (var device in Devices)
                device.ApplyFilter(value);
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task Export()
        {
            var sheets = Devices.SelectMany(d => d.CheckedLeaves()).ToList();
            var blocks = sheets.Where(n => n.Kind == NodeKind.BlockXml)
                .Select(n => new BlockRef { Plc = n.Device, Folders = n.Groups.ToList(), Name = n.Name })
                .ToList();
            var hmiItems = sheets.Where(n => n.Kind == NodeKind.HmiItem)
                .Select(n => new HmiItemRef { Hmi = n.Device, Branch = n.Branch.Value, Folders = n.Groups.ToList(), Name = n.Name })
                .ToList();

            string folder = await ToolContext.Dialogs.ChooseFolder(L.T("Carpeta donde guardar lo exportado"), ToolContext.Settings.LastFolder);
            if (folder == null)
                return;
            RememberFolder(folder);

            await ToolContext.Work(L.T("Exportando…"), async () =>
            {
                var tia = await ToolContext.TiaAsync();
                var progress = ToolContext.TiaProgress();
                var result = new BatchResult();
                if (blocks.Count > 0)
                    Add(result, await tia.ExportBlocksAsync(
                        new ExportBlocksArgs { Blocks = blocks, Folder = ExportFolder(folder, blocks.Select(b => b.Plc), hmiItems.Count > 0) },
                        progress));
                if (hmiItems.Count > 0 && !result.Stopped)
                    Add(result, await tia.ExportHmiAsync(
                        new ExportHmiArgs { Items = hmiItems, Folder = ExportFolder(folder, hmiItems.Select(e => e.Hmi), blocks.Count > 0) },
                        progress));
                ToolContext.ShowResult(result, L.T("{0} elemento(s) exportado(s) a {1}.", result.Succeeded, folder));
            });
        }

        /// <summary>
        /// Carpeta donde exportar los bloques (o los elementos de HMI). Si se exporta de más de un PLC o HMI, cada uno va
        /// en su subcarpeta: TIA-Connector la crea cuando hay varios del mismo tipo; si hay uno solo pero también se
        /// exporta del otro tipo, se pone aquí.
        /// </summary>
        private static string ExportFolder(string folder, IEnumerable<string> devices, bool alsoOtherKind)
        {
            var distinct = devices.Distinct().ToList();
            return alsoOtherKind && distinct.Count == 1 ? Path.Combine(folder, FileRules.SafeName(distinct[0])) : folder;
        }

        private static void Add(BatchResult total, BatchResult part)
        {
            total.Succeeded += part.Succeeded;
            total.Failures.AddRange(part.Failures);
            total.Files.AddRange(part.Files);
            total.Stopped = part.Stopped;
            total.Remaining = part.Remaining;
        }

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ImportFiles()
        {
            var paths = await ToolContext.Dialogs.ChooseFiles(L.T("Archivos a importar"), ToolContext.Settings.LastFolder, FileKind.Xml);
            if (paths == null || paths.Count == 0)
                return;
            RememberFolder(Path.GetDirectoryName(paths[0]));

            if (SelectedNode.IsHmi)
                await ImportIntoHmi(HmiFiles.PrepareFiles(paths, out var discardedHmi), discardedHmi);
            else
                await Import(BlockFiles.PrepareFiles(paths, out var discarded), discarded);
        }

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ImportFolder()
        {
            string folder = await ToolContext.Dialogs.ChooseFolder(L.T("Carpeta con lo que se va a importar (las subcarpetas se crean como carpetas)"), ToolContext.Settings.LastFolder);
            if (folder == null)
                return;
            RememberFolder(folder);

            if (SelectedNode.IsHmi)
                await ImportIntoHmi(HmiFiles.PrepareFolder(folder, out var discardedHmi), discardedHmi);
            else
                await Import(BlockFiles.PrepareFolder(folder, out var discarded), discarded);
        }

        private async Task Import(List<BlockImportFile> files, List<string> discarded)
        {
            var notBlocks = discarded.Select(r => L.T("{0}: no es un bloque exportado de TIA, no se importa", Path.GetFileName(r))).ToList();
            if (files.Count == 0)
            {
                ToolContext.SetFailures(notBlocks);
                ToolContext.ShowStatus(L.T("No hay bloques que importar."), error: true);
                return;
            }

            var target = SelectedNode;
            bool accepted = await ToolContext.Dialogs.Confirm(L.T("Importar bloques"),
                L.T("Se van a importar {0} bloque(s) en {1}.\n\nLos bloques que ya existan con el mismo nombre se sobrescriben.", files.Count, Target),
                L.T("Importar"));
            if (!accepted)
                return;

            await ToolContext.Work(L.T("Importando…"), async () =>
            {
                // Los bloques que ya existen con el mismo nombre en la carpeta destino se sobrescriben.
                var tia = await ToolContext.TiaAsync();
                var result = await tia.ImportBlocksAsync(new ImportBlocksArgs
                {
                    Files = files.Select(a => new ImportFile
                    {
                        Path = a.FilePath, Plc = target.Device, Folders = target.Groups.Concat(a.Groups).ToList()
                    }).ToList()
                }, ToolContext.TiaProgress());
                // Solo se vuelve a leer el PLC destino (leer el proyecto entero tarda minutos en uno grande),
                // y nada si no se había leído.
                if (target.Root.IsRead)
                {
                    ToolContext.ShowStatus(L.T("Leyendo de nuevo lo importado en {0}…", target.Device));
                    await ToolContext.ReadPlcAsync(target.Device);
                    TreeReread(target);
                }
                ToolContext.ShowResult(result, L.T("{0} bloque(s) importado(s). Guarda el proyecto en TIA para conservarlos.", result.Succeeded),
                    importing: true, previous: notBlocks);
            });
        }

        private async Task ImportIntoHmi(List<HmiFile> files, List<string> discarded)
        {
            var notHmi = discarded.Select(r => L.T("{0}: no es un elemento de HMI exportado de TIA, no se importa", Path.GetFileName(r))).ToList();
            if (files.Count == 0)
            {
                ToolContext.SetFailures(notHmi);
                ToolContext.ShowStatus(L.T("No hay elementos de HMI que importar."), error: true);
                return;
            }

            var target = SelectedNode;
            bool accepted = await ToolContext.Dialogs.Confirm(L.T("Importar en la HMI"),
                L.T("Se van a importar {0} elemento(s) en la HMI {1}, cada uno en su rama.\n\nLo que ya exista con el mismo nombre se sobrescribe.", files.Count, target.Root.Name),
                L.T("Importar"));
            if (!accepted)
                return;

            await ToolContext.Work(L.T("Importando…"), async () =>
            {
                // Lo que ya existe con el mismo nombre se sobrescribe; TIA-Connector importa en el orden de las ramas.
                var tia = await ToolContext.TiaAsync();
                var result = await tia.ImportHmiAsync(new ImportHmiArgs
                {
                    Hmi = target.Device,
                    Files = files.Select(a => new HmiImportFile
                    {
                        Path = a.FilePath, Branch = a.Branch, Folders = HmiFiles.TargetFolders(target.Branch, target.Groups, a).ToList()
                    }).ToList()
                }, ToolContext.TiaProgress());
                // Solo se vuelven a leer las ramas de la HMI donde se ha importado algo, y nada si no se había leído.
                if (target.Root.IsRead)
                {
                    ToolContext.ShowStatus(L.T("Leyendo de nuevo lo importado en {0}…", target.Root.Name));
                    var branches = files.Select(a => a.Branch).Distinct().ToList();
                    var readBranches = await tia.ReadHmiBranchesAsync(target.Device, branches);
                    target.Root.ReplaceBranches(branches, readBranches);
                    TreeReread(target);
                }
                ToolContext.ShowResult(result, L.T("{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos.", result.Succeeded),
                    previous: notHmi);
            });
        }

        /// <summary>
        /// Tras releer parte del árbol: vuelve a seleccionar el destino (sus nodos pueden ser otros) y
        /// aplica el filtro y la cuenta de marcados a lo nuevo.
        /// </summary>
        private void TreeReread(TreeNode target)
        {
            SelectedNode = target.Root.Find(target);
            OnPropertyChanged(nameof(ReadText));
            OnFilterChanged(Filter);
            CountChecked();
        }

        private void CountChecked() => CheckedItems = Devices.Sum(d => d.CheckedLeaves().Count());

        private void RememberFolder(string folder)
        {
            ToolContext.Settings.LastFolder = folder;
            ToolContext.Settings.Save();
        }

        private bool CanExport() => CanUseTia && CheckedItems > 0;
        private bool CanImport() => CanUseTia && SelectedNode != null;
        private bool CanRead() => CanUseTia && SelectedNode != null;

        protected override void UpdateCommands()
        {
            ExportCommand.NotifyCanExecuteChanged();
            ImportFilesCommand.NotifyCanExecuteChanged();
            ImportFolderCommand.NotifyCanExecuteChanged();
            ReadCommand.NotifyCanExecuteChanged();
        }
    }
}
