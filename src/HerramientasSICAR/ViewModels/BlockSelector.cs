using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Cómo enseña los bloques un selector.</summary>
    public enum SelectorView
    {
        /// <summary>Todo el árbol del PLC, como en Importar / Exportar.</summary>
        Full,

        /// <summary>Desde la carpeta 10_Sequence &amp; Messageblocks, plegable por OM.</summary>
        Sequences
    }

    /// <summary>
    /// Selector de bloques común a las herramientas: el árbol del proyecto que se lee al conectar (el mismo que el de
    /// Importar / Exportar), con solo los bloques que le valen a la herramienta, buscador y botón para leer un PLC sin
    /// leer. Se elige un bloque (<see cref="BloqueSeleccionado"/>) o varios con casillas (<see cref="Marcados"/>).
    /// </summary>
    public partial class BlockSelector : ObservableObject
    {
        private readonly ToolContext _context;
        private readonly SelectorView _view;
        private readonly Func<IReadOnlyList<string>, BlockInfo, bool> _filter;
        private readonly Func<IReadOnlyList<string>, string, bool> _initiallyChecked;
        private readonly Dictionary<string, bool> _checks = new Dictionary<string, bool>();
        private bool _rebuilding;

        /// <param name="filtro">Bloques que salen: carpetas desde la raíz del PLC y el bloque.</param>
        /// <param name="conCasillas">Elegir varios con casillas (si no, se elige uno seleccionándolo).</param>
        /// <param name="marcadoInicial">Con casillas, si un bloque sale marcado la primera vez que aparece.</param>
        public BlockSelector(ToolContext context, SelectorView view, Func<IReadOnlyList<string>, BlockInfo, bool> filter,
            bool withCheckBoxes = false, Func<IReadOnlyList<string>, string, bool> initiallyChecked = null)
        {
            _context = context;
            _view = view;
            _filter = filter;
            WithCheckBoxes = withCheckBoxes;
            _initiallyChecked = initiallyChecked;
            _context.TreeChanged += _ => Rebuild();
            _context.PropertyChanged += OnContextChanged;
            _context.BusyChanged += () => ReadCommand.NotifyCanExecuteChanged();
            Rebuild();
        }

        public bool WithCheckBoxes { get; }

        /// <summary>Un nodo por PLC del proyecto, con los bloques que valen.</summary>
        public ObservableCollection<TreeNode> Roots { get; } = new ObservableCollection<TreeNode>();

        [ObservableProperty]
        private string _filterText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedBlock), nameof(ReadText))]
        [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
        private TreeNode _selected;

        /// <summary>Se elige otro bloque o cambian las marcas.</summary>
        public event Action Changed;

        /// <summary>El bloque seleccionado, o null si lo seleccionado no es un bloque.</summary>
        public BlockRef SelectedBlock => Selected?.Kind == NodeKind.BlockXml ? Reference(Selected) : null;

        public List<BlockRef> CheckedItems() => Roots.SelectMany(r => r.CheckedLeaves()).Select(Reference).ToList();

        public int CheckedCount => Roots.Sum(r => r.CheckedLeaves().Count());

        public bool HasPlcs => Roots.Count > 0;

        public string ReadText => Selected == null ? L.T("Selecciona un PLC para leerlo")
            : Selected.Root.IsRead ? L.T("Leer de nuevo {0}", Selected.Root.Name)
            : L.T("Leer {0}", Selected.Root.Name);

        /// <summary>Lee de TIA el PLC de lo seleccionado; lo nuevo lo ven todas las herramientas.</summary>
        [RelayCommand(CanExecute = nameof(CanRead))]
        private async Task Read()
        {
            string plc = Selected.Root.Device;
            await _context.Work(L.T("Leyendo el árbol del proyecto…"), async () =>
            {
                await _context.ReadPlcAsync(plc);
                _context.ShowStatus(L.T("Árbol de {0} leído.", plc));
            });
        }

        private bool CanRead() => _context.Connected && !_context.Busy && Selected != null;

        partial void OnFilterTextChanged(string value)
        {
            foreach (var root in Roots)
                root.ApplyFilter(value);
        }

        partial void OnSelectedChanged(TreeNode value)
        {
            if (!_rebuilding)
                Changed?.Invoke();
        }

        private void OnContextChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ToolContext.Connected) || e.PropertyName == nameof(ToolContext.ConnectedProject))
                ReadCommand.NotifyCanExecuteChanged();
        }

        // Rehace el árbol desde el común; conserva la selección y las marcas de lo que ya estaba.
        private void Rebuild()
        {
            _rebuilding = true;
            var selected = Selected;
            Roots.Clear();
            foreach (var device in _context.Devices.Where(d => d.Kind == DeviceKind.Plc))
            {
                var plc = _context.ReadPlc(device.Name);
                if (plc == null)
                {
                    Roots.Add(TreeNode.Unread(device, OnChecked));
                    continue;
                }

                var root = plc.Root;
                var path = new List<string>();
                if (_view == SelectorView.Sequences)
                    root = FindFolder(plc.Root, SequenceComments.SequencesFolder, path);
                var pruned = root == null ? new BlockFolder { Name = plc.Root.Name } : Prune(root, path.ToList());
                var node = TreeNode.FromPlc(new PlcTree { Name = plc.Name, Root = pruned }, OnChecked, path);
                if (root == null)
                    node.Detail = L.T("sin la carpeta {0}", SequenceComments.SequencesFolder);
                else if (pruned.Folders.Count == 0 && pruned.Blocks.Count == 0)
                    node.Detail = L.T("ningún bloque");
                if (WithCheckBoxes)
                    SetChecks(node);
                Roots.Add(node);
            }
            OnFilterTextChanged(FilterText);
            Selected = selected == null ? null : Roots.Select(r => r.Find(selected)).FirstOrDefault(n => n != null);
            _rebuilding = false;
            OnPropertyChanged(nameof(HasPlcs));
            OnPropertyChanged(nameof(CheckedCount));
            Changed?.Invoke();
        }

        // Solo los bloques que valen, y las carpetas que tienen alguno.
        private BlockFolder Prune(BlockFolder group, List<string> path)
        {
            var pruned = new BlockFolder { Name = group.Name };
            foreach (var sub in group.Folders)
            {
                path.Add(sub.Name);
                var child = Prune(sub, path);
                path.RemoveAt(path.Count - 1);
                if (child.Folders.Count > 0 || child.Blocks.Count > 0)
                    pruned.Folders.Add(child);
            }
            foreach (var block in group.Blocks.Where(b => _filter(path, b)))
                pruned.Blocks.Add(block);
            return pruned;
        }

        // La carpeta con ese nombre a cualquier profundidad; deja en «ruta» las carpetas hasta ella (incluida).
        private static BlockFolder FindFolder(BlockFolder group, string name, List<string> path)
        {
            foreach (var sub in group.Folders)
            {
                path.Add(sub.Name);
                if (sub.Name == name)
                    return sub;
                var found = FindFolder(sub, name, path);
                if (found != null)
                    return found;
                path.RemoveAt(path.Count - 1);
            }
            return null;
        }

        // Cada bloque conserva su marca; uno que no estaba sale como diga marcadoInicial.
        private void SetChecks(TreeNode node)
        {
            if (node.IsLeaf)
            {
                string key = Key(Reference(node));
                if (!_checks.TryGetValue(key, out bool isChecked))
                    _checks[key] = isChecked = _initiallyChecked?.Invoke(node.Groups, node.Name) ?? false;
                if (isChecked)
                    node.IsChecked = true;
                return;
            }
            foreach (var child in node.Children)
                SetChecks(child);
        }

        private void OnChecked()
        {
            if (_rebuilding)
                return;
            foreach (var sheet in Roots.SelectMany(AllLeaves))
                _checks[Key(Reference(sheet))] = sheet.IsChecked == true;
            OnPropertyChanged(nameof(CheckedCount));
            Changed?.Invoke();
        }

        private static IEnumerable<TreeNode> AllLeaves(TreeNode node) =>
            node.IsLeaf ? new[] { node } : node.Children.SelectMany(AllLeaves);

        private static BlockRef Reference(TreeNode node) =>
            new BlockRef { Plc = node.Device, Folders = node.Groups.ToList(), Name = node.Name };

        private static string Key(BlockRef b) => b.Plc + "|" + string.Join("/", b.Folders) + "|" + b.Name;
    }
}
