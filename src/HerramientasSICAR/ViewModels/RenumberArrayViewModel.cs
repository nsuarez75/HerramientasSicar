using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Helpers;
using HerramientasSICAR.Logic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Un array del bloque elegido, con sus usos e índices actuales.</summary>
    public sealed class ArrayRow
    {
        public ArrayRow(BlockArray array)
        {
            Array = array;
        }

        public BlockArray Array { get; }
        public string Name => Array.Name;
        public string Uses => L.T("{0} usos", Array.Uses);

        /// <summary>Índices actuales, en el orden en que aparecen (los primeros, si son muchos).</summary>
        public string Indexes
        {
            get
            {
                const int maximum = 40;
                string list = string.Join(", ", Array.Indexes.Take(maximum));
                return Array.Indexes.Count > maximum ? list + ", …" : list;
            }
        }
    }

    public partial class RenumberArrayViewModel : Tool
    {
        // Para no leer dos veces el mismo bloque.
        private BlockRef _isRead;

        public RenumberArrayViewModel(ToolContext context) : base(context)
        {
            Selector = new BlockSelector(context, SelectorView.Full,
                (_, b) => (b.Type == "FB" || b.Type == "FC") && !b.IsProtected);
            Selector.Changed += OnBlockChanged;
            _blockStatus = L.T("Elige en el árbol el FB o FC.");
        }

        public override string Name => L.T("Renumerar Array");
        public override string Description => L.T("Renumera de menor a mayor todos los usos de un array dentro de un FB o FC. Útil, por ejemplo, para intercalar un segmento nuevo en un array de flancos sin reordenarlo a mano. No es exclusivo de SICAR.");
        public override string Icon => "list-ordered";
        public override bool UsesTia => true;

        public BlockSelector Selector { get; }

        public ObservableCollection<ArrayRow> Arrays { get; } = new ObservableCollection<ArrayRow>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenumberCommand))]
        private ArrayRow _selectedArray;

        [ObservableProperty]
        private string _blockStatus;

        public string BlockName => Selector.SelectedBlock?.Name;
        public bool HasBlock => Selector.SelectedBlock != null;
        public bool HasArrays => Arrays.Count > 0;

        private static bool SameBlock(BlockRef a, BlockRef b) =>
            a != null && b != null && a.Plc == b.Plc && a.Name == b.Name && a.Folders.SequenceEqual(b.Folders);

        // Al elegir un bloque se exporta (a una carpeta temporal) y se listan sus arrays con índice fijo.
        private async void OnBlockChanged()
        {
            OnPropertyChanged(nameof(BlockName));
            OnPropertyChanged(nameof(HasBlock));
            RenumberCommand.NotifyCanExecuteChanged();
            var block = Selector.SelectedBlock;
            if (block == null)
            {
                _isRead = null;
                SetArrays(null);
                BlockStatus = L.T("Elige en el árbol el FB o FC.");
                return;
            }
            if (SameBlock(_isRead, block) || !CanUseTia)
                return;
            await ReadArrays(block);
        }

        private async Task ReadArrays(BlockRef block)
        {
            _isRead = block;
            SetArrays(null);
            BlockStatus = L.T("Leyendo {0}…", block.Name);
            await ToolContext.Work(L.T("Leyendo {0}…", block.Name), async () =>
            {
                string xml = await (await ToolContext.TiaAsync()).ReadBlockXmlAsync(block);
                var arrays = await Task.Run(() => BlockArrays.ListArrays(xml));
                if (!SameBlock(Selector.SelectedBlock, block))
                    return;
                SetArrays(arrays);
                BlockStatus = arrays.Count == 0
                    ? L.T("{0} no usa ningún array con índice fijo.", block.Name)
                    : L.T("Elige el array que quieres renumerar.");
                ToolContext.ShowStatus(L.T("{0}: {1} array(s) con índice fijo.", block.Name, arrays.Count));
            });
            if (Arrays.Count == 0 && SameBlock(_isRead, block) && ToolContext.StatusError)
            {
                _isRead = null;
                BlockStatus = ToolContext.Status;
            }
        }

        private void SetArrays(IReadOnlyList<BlockArray> arrays)
        {
            Arrays.Clear();
            foreach (var array in arrays ?? new BlockArray[0])
                Arrays.Add(new ArrayRow(array));
            SelectedArray = Arrays.Count == 1 ? Arrays[0] : null;
            OnPropertyChanged(nameof(HasArrays));
        }

        [RelayCommand(CanExecute = nameof(CanRenumber))]
        private async Task Renumber()
        {
            var block = Selector.SelectedBlock;
            string array = SelectedArray.Name;
            bool done = false;
            int changes = 0;
            await ToolContext.Work(L.T("Exportando {0}…", block.Name), async () =>
            {
                // Se exporta, se renumera y se importa encima del original, en su mismo grupo.
                var tia = await ToolContext.TiaAsync();
                var result = await tia.ModifyBlocksAsync(new[] { block },
                    (_, xml) => Task.FromResult(BlockArrays.Renumber(xml, array, out changes)), compile: false, ToolContext.TiaProgress());
                var failures = TiaTexts.Failures(result, importing: true);
                if (failures.Count > 0)
                {
                    ToolContext.SetFailures(failures);
                    ToolContext.ShowStatus(failures[0], error: true);
                    return;
                }
                done = true;
            });
            if (!done)
                return;

            // Se vuelve a leer el bloque para ver los índices nuevos.
            await ReadArrays(block);
            SelectedArray = Arrays.FirstOrDefault(a => a.Name == array) ?? SelectedArray;
            ToolContext.ShowStatus(L.T("{0}: {1} uso(s) de {2} renumerados. Guarda el proyecto en TIA para conservarlos.", block.Name, changes, array));
        }

        private bool CanRenumber() => CanUseTia && Selector.SelectedBlock != null && SelectedArray != null;

        protected override void UpdateCommands() => RenumberCommand.NotifyCanExecuteChanged();
    }
}
