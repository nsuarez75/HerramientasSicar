using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    public enum NodeKind
    {
        Plc,
        Hmi,
        Branch,
        Folder,
        BlockXml,
        HmiItem
    }

    /// <summary>
    /// Un nodo del árbol: PLC o HMI, rama de HMI, carpeta, bloque o elemento de HMI.
    /// Marcar un nodo marca todo lo que tiene dentro.
    /// </summary>
    public partial class TreeNode : ObservableObject
    {
        private readonly Action _onChecksChanged;
        private bool _propagating;

        private TreeNode(NodeKind kind, string device, HmiBranch? branch, IReadOnlyList<string> groups, string name, string detail,
            TreeNode parent, Action onChecksChanged)
        {
            Kind = kind;
            Device = device;
            Branch = branch;
            Groups = groups;
            Name = name;
            Detail = detail;
            Parent = parent;
            _onChecksChanged = onChecksChanged;
        }

        public NodeKind Kind { get; }

        /// <summary>Nombre del PLC o de la HMI al que pertenece el nodo.</summary>
        public string Device { get; }

        /// <summary>Rama de la HMI (null en los PLC y en el propio nodo de la HMI).</summary>
        public HmiBranch? Branch { get; }

        /// <summary>
        /// Carpetas hasta este nodo: en un PLC desde "Bloques de programa"; en una HMI, dentro de su rama.
        /// Para un bloque o un elemento, las de la carpeta donde está.
        /// </summary>
        public IReadOnlyList<string> Groups { get; }

        public string Name { get; }
        public TreeNode Parent { get; }

        [ObservableProperty]
        private string _detail;

        /// <summary>False en un PLC o una HMI cuyo contenido no se ha leído de TIA (se eligió no leerlo al conectar).</summary>
        [ObservableProperty]
        private bool _isRead = true;
        public bool IsLeaf => Kind == NodeKind.BlockXml || Kind == NodeKind.HmiItem;
        public bool IsHmi => Kind == NodeKind.Hmi || Branch != null;
        public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();

        public string Icon
        {
            get
            {
                switch (Kind)
                {
                    case NodeKind.Plc: return "cpu";
                    case NodeKind.Hmi: return "monitor";
                    case NodeKind.BlockXml: return "box";
                    case NodeKind.HmiItem: return Branch == HmiBranch.Tags ? "list-ordered" : Branch == HmiBranch.Scripts ? "file-code" : "layers";
                    default: return "folder";
                }
            }
        }

        [ObservableProperty]
        private bool? _isChecked = false;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _visible = true;

        /// <summary>
        /// Árbol de un PLC. <paramref name="alCambiarMarcas"/> se llama cada vez que el usuario marca o desmarca algo.
        /// Con <paramref name="gruposRaiz"/>, <see cref="PlcTree.Root"/> es esa carpeta del PLC y no la raíz de bloques
        /// (los selectores de las herramientas que empiezan en la carpeta de secuencias).
        /// </summary>
        public static TreeNode FromPlc(PlcTree plc, Action onChecksChanged, IReadOnlyList<string> rootFolders = null)
        {
            var node = new TreeNode(NodeKind.Plc, plc.Name, null, rootFolders ?? new string[0], plc.Name, null, null, onChecksChanged) { IsExpanded = true };
            node.AddContent(plc.Root);
            return node;
        }

        /// <summary>
        /// Árbol de una HMI: sus ramas, con las únicas (elementos globales, vista general) como elementos sueltos.
        /// <paramref name="nombreVisible"/> es el que se enseña (en un IPC, el de la estación); <see cref="Dispositivo"/>
        /// sigue siendo el nombre de la HMI, que es con el que se habla con TIA.
        /// </summary>
        public static TreeNode FromHmi(HmiTree hmi, Action onChecksChanged, string displayName = null)
        {
            var node = new TreeNode(NodeKind.Hmi, hmi.Name, null, new string[0], displayName ?? hmi.Name, null, null, onChecksChanged) { IsExpanded = true };
            foreach (var branch in hmi.Branches)
                node.Children.Add(node.BranchNode(branch));
            return node;
        }

        /// <summary>Un PLC o una HMI sin su contenido, que se lee después con <see cref="ReemplazarContenido"/> o <see cref="ReemplazarRamas"/>.</summary>
        public static TreeNode Unread(DeviceInfo device, Action onChecksChanged, string displayName = null) =>
            new TreeNode(device.Kind == DeviceKind.Hmi ? NodeKind.Hmi : NodeKind.Plc, device.Name, null, new string[0],
                displayName ?? device.Name, L.T("sin leer"), null, onChecksChanged) { IsRead = false };

        /// <summary>El PLC o la HMI del que cuelga este nodo.</summary>
        public TreeNode Root => Parent?.Root ?? this;

        /// <summary>
        /// Cambia los bloques de este PLC por los recién leídos. Lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReplaceContent(BlockFolder root)
        {
            var previous = Children.ToList();
            Children.Clear();
            AddContent(root);
            CopyChildrenState(previous);
            AdjustCheck();
            MarkRead();
        }

        /// <summary>
        /// Cambia esas ramas de esta HMI por las recién leídas (<paramref name="leidas"/>: las que el panel tiene).
        /// Las demás ramas no se tocan; lo que ya estaba conserva sus marcas y sus carpetas abiertas.
        /// </summary>
        public void ReplaceBranches(IEnumerable<HmiBranch> branches, IReadOnlyList<HmiBranchTree> readBranches)
        {
            foreach (var branch in branches)
            {
                var previousFile = Children.FirstOrDefault(h => h.Branch == branch);
                if (previousFile != null)
                    Children.Remove(previousFile);

                var info = readBranches.FirstOrDefault(r => r.Branch == branch);
                if (info == null)
                    continue;
                var created = BranchNode(info);
                if (previousFile != null)
                    CopyState(previousFile, created);

                int order = Order(branch);
                int position = Children.Count(h => Order(h.Branch.Value) < order);
                Children.Insert(position, created);
            }
            AdjustCheck();
            MarkRead();
        }

        private void MarkRead()
        {
            if (IsRead)
                return;
            IsRead = true;
            Detail = null;
            IsExpanded = true;
        }

        /// <summary>El nodo de este árbol que está donde estaba <paramref name="modelo"/> (mismo tipo, rama, carpetas y nombre).</summary>
        public TreeNode Find(TreeNode model)
        {
            if (Kind == model.Kind && Device == model.Device && Branch == model.Branch && Name == model.Name
                && Groups.SequenceEqual(model.Groups))
                return this;
            return Children.Select(h => h.Find(model)).FirstOrDefault(n => n != null);
        }

        private static int Order(HmiBranch branch)
        {
            for (int i = 0; i < HmiRules.TreeOrder.Count; i++)
                if (HmiRules.TreeOrder[i] == branch)
                    return i;
            return HmiRules.TreeOrder.Count;
        }

        private TreeNode BranchNode(HmiBranchTree branch)
        {
            string name = L.T(HmiRules.FolderName(branch.Branch));
            if (HmiRules.IsSingleItem(branch.Branch))
                return new TreeNode(NodeKind.HmiItem, Device, branch.Branch, new string[0], name, null, this, _onChecksChanged);

            var child = new TreeNode(NodeKind.Branch, Device, branch.Branch, new string[0], name, null, this, _onChecksChanged);
            child.AddContent(branch.Root);
            return child;
        }

        /// <summary>Pasa al nodo recién leído si estaba abierto y marcado, y lo mismo a lo que tiene dentro.</summary>
        private static void CopyState(TreeNode previousFile, TreeNode created)
        {
            created.IsExpanded = previousFile.IsExpanded;
            if (created.IsLeaf)
            {
                created._propagating = true;
                created.IsChecked = previousFile.IsChecked;
                created._propagating = false;
                return;
            }
            created.CopyChildrenState(previousFile.Children);
            created.AdjustCheck();
        }

        private void CopyChildrenState(IEnumerable<TreeNode> previous)
        {
            foreach (var child in Children)
            {
                var previousFile = previous.FirstOrDefault(a => a.Kind == child.Kind && a.Name == child.Name);
                if (previousFile != null)
                    CopyState(previousFile, child);
            }
        }

        private void AddContent(BlockFolder group)
        {
            foreach (var subgroup in group.Folders)
            {
                var child = new TreeNode(NodeKind.Folder, Device, null, Groups.Concat(new[] { subgroup.Name }).ToList(), subgroup.Name, null, this, _onChecksChanged);
                child.AddContent(subgroup);
                Children.Add(child);
            }
            foreach (var block in group.Blocks)
            {
                string detail = $"{block.Type}{block.Number}" + (block.IsProtected ? " · " + L.T("protegido") : "");
                Children.Add(new TreeNode(NodeKind.BlockXml, Device, null, Groups, block.Name, detail, this, _onChecksChanged));
            }
        }

        private void AddContent(HmiFolder folder)
        {
            foreach (var subfolder in folder.Folders)
            {
                var child = new TreeNode(NodeKind.Folder, Device, Branch, Groups.Concat(new[] { subfolder.Name }).ToList(), subfolder.Name, null, this, _onChecksChanged);
                child.AddContent(subfolder);
                Children.Add(child);
            }
            foreach (var item in folder.Items)
                Children.Add(new TreeNode(NodeKind.HmiItem, Device, Branch, Groups, item, null, this, _onChecksChanged));
        }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (_propagating || value == null)
                return;

            CheckChildren(value);
            Parent?.RecalculateCheck();
            _onChecksChanged?.Invoke();
        }

        private void CheckChildren(bool? value)
        {
            // Con el filtro puesto solo se marca lo que se ve.
            foreach (var child in Children.Where(h => h.Visible))
            {
                child._propagating = true;
                child.IsChecked = value;
                child._propagating = false;
                child.CheckChildren(value);
            }
        }

        private void RecalculateCheck()
        {
            AdjustCheck();
            Parent?.RecalculateCheck();
        }

        /// <summary>Pone la marca de este nodo según la de sus hijos, sin avisar ni tocar a los demás.</summary>
        private void AdjustCheck()
        {
            bool? mark = Children.Count == 0 ? IsChecked
                : Children.All(h => h.IsChecked == true) ? true
                : Children.All(h => h.IsChecked == false) ? false
                : (bool?)null;

            _propagating = true;
            IsChecked = mark;
            _propagating = false;
        }

        /// <summary>Bloques y elementos de HMI marcados de este nodo hacia abajo.</summary>
        public IEnumerable<TreeNode> CheckedLeaves()
        {
            if (IsLeaf)
                return IsChecked == true ? new[] { this } : Enumerable.Empty<TreeNode>();
            return Children.SelectMany(h => h.CheckedLeaves());
        }

        /// <summary>Deja visibles solo los bloques y elementos cuyo nombre contiene el texto (y las carpetas que los contienen).</summary>
        public bool ApplyFilter(string text)
        {
            bool hasText = !string.IsNullOrWhiteSpace(text);
            bool matches = IsLeaf && (!hasText || Name.IndexOf(text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            bool anyChild = false;
            foreach (var child in Children)
                anyChild |= child.ApplyFilter(text);

            Visible = matches || anyChild || (!IsLeaf && !hasText);
            if (hasText && anyChild)
                IsExpanded = true;
            return Visible;
        }
    }
}
