using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HerramientasSICAR.Logic;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Programs;
using HerramientasSICAR.Core.Sicar;
using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaConnector.Contracts;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Secuencia con pares de pasos que pueden sincronizar a la vez.</summary>
    public sealed class SequenceRow
    {
        public SequenceRow(SequenceBlock sequence, string zone, IReadOnlyList<PairRow> pairList)
        {
            SequenceBlock = sequence;
            Zone = zone;
            Pairs = pairList;
        }

        public SequenceBlock SequenceBlock { get; }
        public string Zone { get; }
        public IReadOnlyList<PairRow> Pairs { get; }
        public string Text => $"{SequenceBlock.Sequence}  ·  {SequenceBlock.Name}";
        public string Count => Pairs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Nodo del árbol de resultados: un OM con sus secuencias, o una secuencia.</summary>
    public sealed partial class ResultNode : ObservableObject
    {
        public ResultNode(string text, int count, SequenceRow sequence = null)
        {
            Text = text;
            Count = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Sequence = sequence;
        }

        public string Text { get; }
        public string Count { get; }

        /// <summary>La secuencia, o null en un nodo de OM.</summary>
        public SequenceRow Sequence { get; }
        public ObservableCollection<ResultNode> Children { get; } = new ObservableCollection<ResultNode>();

        [ObservableProperty]
        private bool _isExpanded;
    }

    public sealed class PairRow
    {
        public PairRow(SyncConflict conflictItem, string stepA, string stepB)
        {
            Conflict = conflictItem;
            StepAName = stepA;
            StepBName = stepB;
        }

        public SyncConflict Conflict { get; }
        public string StepAName { get; }
        public string StepBName { get; }
        public string Pair => $"{Conflict.StepA.Label} ↔ {Conflict.StepB.Label}";
    }

    /// <summary>Señal que se puede marcar como excluyente en un par.</summary>
    public sealed partial class CandidateRow : ObservableObject
    {
        public CandidateRow(string signal, string comment, bool value = true)
        {
            Signal = signal;
            CommentText = comment;
            _value = value;
        }

        public string Signal { get; }
        public string CommentText { get; }
        public string Name => SignalText.Name(Signal);

        [ObservableProperty]
        private bool _isChecked;

        /// <summary>Valor de la señal en la condición: 1 o 0.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ValueText))]
        private bool _value;

        public string ValueText => Value ? "= 1" : "= 0";

        [RelayCommand]
        private void ToggleValue() => Value = !Value;
    }

    /// <summary>Escalón de un paso con el estado de señales que se está mirando.</summary>
    public sealed class RungRow
    {
        public RungRow(LadRung rung, IReadOnlyDictionary<string, bool> state)
        {
            Rung = rung;
            State = state;
        }

        public LadRung Rung { get; }
        public IReadOnlyDictionary<string, bool> State { get; }
    }

    /// <summary>Un paso del par: si es candidato con el estado y los escalones que lo deciden.</summary>
    public sealed class StepLadderRow
    {
        public StepLadderRow(string title, bool? candidate, IReadOnlyList<RungRow> rungs)
        {
            Title = title;
            Candidate = candidate;
            Rungs = rungs;
        }

        public string Title { get; }
        public bool? Candidate { get; }
        public IReadOnlyList<RungRow> Rungs { get; }
        public bool IsCandidate => Candidate == true;
        public bool IsNotCandidate => Candidate == false;
        public bool IsUnknown => Candidate == null;
        public bool HasNoRungs => Rungs.Count == 0;
    }

    public sealed class GroupRow
    {
        public GroupRow(ExclusiveGroup group)
        {
            Group = group;
        }

        public ExclusiveGroup Group { get; }
        public string Text => string.Join("  ·  ", Group.Signals.Select((s, i) => SignalText.Name(s) + (Group.Value(i) ? " = 1" : " = 0")));
        public string Note => Group.Note ?? "";
    }

    /// <summary>Las señales del análisis van con el texto del operando: "Variable"."Miembro".</summary>
    public static class SignalText
    {
        private const string Separator = "\".\"";

        public static string[] Components(string signal) => signal.Trim('"').Split(new[] { Separator }, StringSplitOptions.None);

        public static string Name(string signal) => string.Join(".", Components(signal));
    }

    /// <summary>
    /// Doble sincronización de un PLC (Core/Sicar/SyncChecker.cs): pares de
    /// pasos que pueden sincronizar a la vez y decisiones del usuario sobre señales excluyentes. Al exportar desde TIA,
    /// el programa va a %TEMP% y las decisiones a AppData (<see cref="SyncFile.InAppData"/>); al abrir una carpeta
    /// exportada a mano, al sincro.json de esa carpeta.
    /// </summary>
    public partial class SyncCheckerViewModel : Tool
    {
        private PlcProgram _program;
        private SyncFile _file = new SyncFile();

        public SyncCheckerViewModel(ToolContext context) : base(context)
        {
        }

        public override string Name => "Sincro Checker";
        public override string Description => L.T("Busca en el programa de un PLC los pares de pasos de cada secuencia que pueden ser candidatos a la vez al pasar de manual a automático, lo que da error de sincronización.");
        public override string Icon => "git-compare";

        // Puede abrir una carpeta ya exportada sin conexión; solo «Exportar desde TIA» necesita el proyecto.
        public override bool UsesTia => false;

        public ObservableCollection<SequenceRow> Sequences { get; } = new ObservableCollection<SequenceRow>();

        /// <summary>Secuencias con pares, agrupadas por OM.</summary>
        public ObservableCollection<ResultNode> ResultTree { get; } = new ObservableCollection<ResultNode>();

        [ObservableProperty]
        private ResultNode _selectedResult;
        public ObservableCollection<PairRow> Pairs { get; } = new ObservableCollection<PairRow>();
        public ObservableCollection<CandidateRow> CandidateRows { get; } = new ObservableCollection<CandidateRow>();
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();
        public ObservableCollection<StepLadderRow> Ladder { get; } = new ObservableCollection<StepLadderRow>();

        // Estado de señales del par elegido: el que encontró la comprobación, con los cambios que haga el usuario.
        private Dictionary<string, bool> _state = new Dictionary<string, bool>();

        [ObservableProperty]
        private bool _stateChanged;

        /// <summary>El panel de la decisión sobre el par está desplegado.</summary>
        [ObservableProperty]
        private bool _decisionExpanded = true;

        /// <summary>Carpeta del programa exportado que se está analizando.</summary>
        [ObservableProperty]
        private string _folder = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasProgram))]
        private bool _loading;

        [ObservableProperty]
        private string _summary = "";

        [ObservableProperty]
        private bool _summaryWithPairs;

        [ObservableProperty]
        private SequenceRow _selectedSequence;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPair), nameof(NoCandidates))]
        private PairRow _selectedPair;

        public bool HasProgram => _program != null && !Loading;
        public bool HasPairs => HasProgram && Sequences.Count > 0;
        public bool HasPair => SelectedPair != null;
        public bool HasCandidates => CandidateRows.Count > 0;
        public bool NoCandidates => HasPair && CandidateRows.Count == 0;
        public bool HasGroups => HasProgram && Groups.Count > 0;
        public string GroupsTitle => L.T("Señales excluyentes guardadas ({0})", Groups.Count);

        [RelayCommand(CanExecute = nameof(Idle))]
        private async Task OpenFolder()
        {
            string folder = await ToolContext.Dialogs.ChooseFolder(L.T("Carpeta con el programa exportado del PLC"),
                string.IsNullOrEmpty(_openedFolder) ? null : _openedFolder);
            if (folder == null)
                return;
            _openedFolder = folder;
            await Load(folder, folder, SyncFile.InFolder(folder), null);
        }

        // Última carpeta abierta a mano, para empezar ahí el diálogo.
        private string _openedFolder;

        // Dónde se guardan las decisiones del programa cargado.
        private string _filePath;

        [RelayCommand(CanExecute = nameof(CanUseTia))]
        private async Task ExportFromTia()
        {
            string plc = await ChoosePlc(L.T("Exportar desde TIA"), L.T("Exportar"));
            if (plc == null)
                return;

            // Se exporta a %TEMP% y se borra en cuanto se ha leído: en la carpeta del proyecto no queda nada.
            using (var temp = new TempFolder())
            {
                var info = ToolContext.ProjectFile;
                bool exported = false;
                var skipped = new List<string>();
                await ToolContext.Work(L.T("Exportando el programa de {0}…", plc), async () =>
                {
                    // Bloques, tablas de variables y tipos, sin compilar nada: lo protegido o sin compilar se salta.
                    var result = await (await ToolContext.TiaAsync()).ExportProgramAsync(plc, temp.Path, ToolContext.TiaProgress());
                    skipped.AddRange(TiaTexts.Failures(result));
                    exported = !result.Stopped;
                    if (!exported)
                    {
                        ToolContext.SetFailures(skipped);
                        ToolContext.ShowStatus(L.T("TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer.", result.Remaining), error: true);
                    }
                });
                if (!exported)
                    return;

                // Antes la exportación y el sincro.json se dejaban en la carpeta del proyecto.
                string previousFile = SyncFile.InFolder(Path.Combine(info.ProjectFolder, "HerramientasSICAR", "Sincro", FileRules.SafeName(plc)));
                string path = SyncFile.InAppData(info.Name, plc);
                BringPreviousExclusive(previousFile, path);
                await Load(temp.Path, L.T("{0} · {1} (exportado de TIA)", info.Name, plc), path, info.EditingLanguage);
                if (skipped.Count > 0 && HasProgram)
                {
                    ToolContext.SetFailures(skipped.Concat(_program.Warnings));
                    ToolContext.ShowStatus(L.T("{0}. {1} objeto(s) no se han exportado (ver abajo): sus señales cuentan como libres.", Summary, skipped.Count), error: true);
                }
            }
        }

        // Las excluyentes del sincro.json que dejaban las versiones anteriores en la carpeta del proyecto, si aún no hay
        // archivo en AppData.
        private static void BringPreviousExclusive(string previousFile, string path)
        {
            try
            {
                if (File.Exists(path) || !File.Exists(previousFile))
                    return;
                SyncFile.Load(previousFile).Save(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Se empieza sin decisiones.
            }
        }

        // Lee el programa de la carpeta y calcula los pares; las decisiones se leen y se guardan en rutaArchivo. El idioma
        // de edición viene de TIA al exportar; al abrir una carpeta, del sincro.json que se dejó entonces.
        private async Task Load(string folder, string source, string filePath, string editingLanguage)
        {
            _program = null;
            Folder = source;
            _filePath = filePath;
            Sequences.Clear();
            Pairs.Clear();
            CandidateRows.Clear();
            Groups.Clear();
            UpdateView();

            await ToolContext.Work(L.T("Leyendo el programa exportado…"), async () =>
            {
                Loading = true;
                try
                {
                    _file = SyncFile.Load(filePath);
                    if (!string.IsNullOrWhiteSpace(editingLanguage) && editingLanguage != _file.EditingLanguage)
                    {
                        _file.EditingLanguage = editingLanguage;
                        _file.Save(filePath);
                    }
                    string language = _file.EditingLanguage;
                    var program = await Task.Run(() => PlcProgram.Load(folder, language));
                    _program = program;
                    await Analyze(null, null);
                    ToolContext.SetFailures(program.Warnings);
                    ToolContext.ShowStatus(Summary, error: SummaryWithPairs);
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                {
                    _program = null;
                    ToolContext.ShowStatus(L.T("No se pudo leer la exportación: {0}", e.Message), error: true);
                }
                finally
                {
                    Loading = false;
                    UpdateView();
                }
            });
        }

        // Calcula los pares de todas las secuencias con las decisiones guardadas y vuelve a donde estaba.
        private async Task Analyze(int? sequence, string pair)
        {
            var program = _program;
            var groups = _file.Groups();
            string language = L.Language == L.English ? "english" : "espanol";
            var rows = await Task.Run(() =>
            {
                var checker = program.SyncChecker(groups);
                return program.Sequences
                    .Select(s => new SequenceRow(s, Zone(program, s),
                        checker.Check(s).Select(c => new PairRow(c, StepNetwork(c.StepA, language), StepNetwork(c.StepB, language))).ToList()))
                    .Where(f => f.Pairs.Count > 0)
                    .ToList();
            });

            Sequences.Clear();
            foreach (var row in rows)
                Sequences.Add(row);
            BuildTree(rows);
            Groups.Clear();
            foreach (var group in groups)
                Groups.Add(new GroupRow(group));

            int total = rows.Sum(f => f.Pairs.Count);
            SummaryWithPairs = total > 0;
            Summary = total == 0
                ? L.T("{0} secuencias: ningún par de pasos puede sincronizar a la vez", program.Sequences.Count)
                : L.T("{0} pares posibles en {1} de {2} secuencias", total, rows.Count, program.Sequences.Count);

            SelectedSequence = rows.FirstOrDefault(f => f.SequenceBlock.Sequence == sequence) ?? rows.FirstOrDefault();
            SelectedPair = Pairs.FirstOrDefault(p => p.Pair == pair) ?? Pairs.FirstOrDefault();
            UpdateView();
        }

        private static string Zone(PlcProgram program, SequenceBlock sequence) =>
            program.OperationModeOf(sequence.Sequence) is int om ? $"OM{om}" : "*";

        // Un nodo por OM (en orden; las secuencias sin OM, al final) con sus secuencias. Los OM salen plegados salvo el
        // de la secuencia elegida.
        private void BuildTree(IReadOnlyList<SequenceRow> rows)
        {
            ResultTree.Clear();
            foreach (var zone in rows.GroupBy(r => r.Zone)
                         .OrderBy(g => g.Key == "*" ? int.MaxValue : int.TryParse(g.Key.Substring(2), out int n) ? n : int.MaxValue - 1))
            {
                var node = new ResultNode(zone.Key == "*" ? L.T("Sin OM") : zone.Key, zone.Sum(r => r.Pairs.Count));
                foreach (var row in zone)
                    node.Children.Add(new ResultNode(row.Text, row.Pairs.Count, row));
                ResultTree.Add(node);
            }
        }

        partial void OnSelectedResultChanged(ResultNode value)
        {
            if (value?.Sequence != null && value.Sequence != SelectedSequence)
                SelectedSequence = value.Sequence;
        }

        private static string StepNetwork(SequenceStep step, string language) => $"{step.Label}  {step.Title(language)}";

        partial void OnSelectedSequenceChanged(SequenceRow value)
        {
            // El árbol sigue a la secuencia elegida (también al recalcular): se despliega su OM.
            foreach (var zone in ResultTree)
            {
                var node = zone.Children.FirstOrDefault(n => n.Sequence == value);
                if (node == null)
                    continue;
                zone.IsExpanded = true;
                if (SelectedResult != node)
                    SelectedResult = node;
            }
            Pairs.Clear();
            foreach (var pair in value?.Pairs ?? (IReadOnlyList<PairRow>)Array.Empty<PairRow>())
                Pairs.Add(pair);
            SelectedPair = Pairs.FirstOrDefault();
        }

        partial void OnSelectedPairChanged(PairRow value)
        {
            _state = value == null ? new Dictionary<string, bool>() : new Dictionary<string, bool>(value.Conflict.State);
            StateChanged = false;
            BuildLadder();
            CandidateRows.Clear();
            if (value != null && SelectedSequence != null && _program != null)
            {
                var checker = _program.SyncChecker(_file.Groups());
                foreach (string signal in checker.ExclusionCandidates(value.Conflict, SelectedSequence.SequenceBlock.Permanent))
                    AddCandidateRow(signal);
            }
            CandidatesChanged();
        }

        // Clic derecho en una instrucción del ladder: su señal pasa, marcada, a la lista de excluyentes del par (si ya
        // estaba, se marca), con el valor que pide la instrucción (0 si es un contacto negado), para formar a mano un grupo
        // con las propuestas o con otras señales. El panel se despliega para verla.
        [RelayCommand]
        private void AddCandidate(LadExpression leaf)
        {
            string signal = leaf == null ? null : SyncState.Signal(leaf);
            if (SelectedPair == null || string.IsNullOrEmpty(signal) || SyncChecker.FixedValue(signal).HasValue)
                return;
            bool value = !(leaf is ContactExpression contact && contact.Negated);
            var row = CandidateRows.FirstOrDefault(c => c.Signal == signal) ?? AddCandidateRow(signal, value);
            row.Value = value;
            row.IsChecked = true;
            DecisionExpanded = true;
            CandidatesChanged();
        }

        private CandidateRow AddCandidateRow(string signal, bool value = true)
        {
            var candidate = new CandidateRow(signal, CommentText(signal), value);
            candidate.PropertyChanged += (_, __) => MarkExclusiveCommand.NotifyCanExecuteChanged();
            CandidateRows.Add(candidate);
            return candidate;
        }

        private void CandidatesChanged()
        {
            MarkExclusiveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasCandidates));
            OnPropertyChanged(nameof(NoCandidates));
        }

        // Cambia una señal del estado (si el estado no la decía, pasa a 1) para ver qué paso deja de ser candidato.
        [RelayCommand]
        private void ToggleSignal(string signal)
        {
            if (SelectedPair == null || string.IsNullOrEmpty(signal) || SyncChecker.FixedValue(signal).HasValue)
                return;
            _state[signal] = !(_state.TryGetValue(signal, out bool value) && value);
            StateChanged = true;
            BuildLadder();
        }

        [RelayCommand]
        private void ResetState()
        {
            if (SelectedPair == null)
                return;
            _state = new Dictionary<string, bool>(SelectedPair.Conflict.State);
            StateChanged = false;
            BuildLadder();
        }

        private void BuildLadder()
        {
            Ladder.Clear();
            var pair = SelectedPair;
            if (pair == null || SelectedSequence == null)
                return;
            var permanent = SelectedSequence.SequenceBlock.Permanent;
            var state = new Dictionary<string, bool>(_state);
            foreach (var row in new[] { (pair.Conflict.StepA, pair.StepAName), (pair.Conflict.StepB, pair.StepBName) })
            {
                Ladder.Add(new StepLadderRow(row.Item2, SyncState.IsCandidate(row.Item1, permanent, state),
                    StepRungs.ForSync(row.Item1).Select(r => new RungRow(r, state)).ToList()));
            }
        }

        // Comentario de una señal simple para ayudar a decidir; las comparaciones no tienen.
        private string CommentText(string signal)
        {
            var info = _program.Symbols.Find(new Operand(OperandScope.Global, SignalText.Components(signal), signal));
            return info?.Comment.Get(_file.EditingLanguage) ?? "";
        }

        // Al menos dos señales para que la decisión diga algo.
        private bool CanMark() => Idle && CandidateRows.Count(c => c.IsChecked) >= 2;

        [RelayCommand(CanExecute = nameof(CanMark))]
        private async Task MarkExclusive()
        {
            var pair = SelectedPair;
            var row = SelectedSequence;
            if (pair == null || row == null)
                return;

            var chosen = CandidateRows.Where(c => c.IsChecked).ToList();
            var group = new ExclusiveGroup(chosen.Select(c => c.Signal).ToList())
            {
                Values = chosen.All(c => c.Value) ? null : chosen.Select(c => c.Value).ToList(),
                Note = L.T("Secuencia {0}: {1}", row.SequenceBlock.Sequence, pair.Pair),
            };
            int position = Pairs.IndexOf(pair);
            if (!SaveGroups(_file.Groups().Concat(new[] { group })))
                return;
            await Analyze(row.SequenceBlock.Sequence, null);
            // Se sigue en el par que ha pasado a ocupar ese sitio, para revisar la lista de corrido.
            SelectedPair = Pairs.ElementAtOrDefault(Math.Min(position, Pairs.Count - 1));
        }

        [RelayCommand]
        private async Task DeleteGroup(GroupRow row)
        {
            if (row == null)
                return;
            if (!SaveGroups(_file.Groups().Where(g => !g.Equals(row.Group))))
                return;
            await Analyze(SelectedSequence?.SequenceBlock.Sequence, SelectedPair?.Pair);
        }

        private bool SaveGroups(IEnumerable<ExclusiveGroup> groups)
        {
            var previous = _file.Groups();
            _file.SetGroups(groups.ToList());
            try
            {
                _file.Save(_filePath);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _file.SetGroups(previous);
                ToolContext.ShowStatus(L.T("No se pudo guardar la decisión: {0}", e.Message), error: true);
                return false;
            }
        }

        private void UpdateView()
        {
            OnPropertyChanged(nameof(HasProgram));
            OnPropertyChanged(nameof(HasPairs));
            OnPropertyChanged(nameof(HasGroups));
            OnPropertyChanged(nameof(GroupsTitle));
        }

        protected override void UpdateCommands()
        {
            OpenFolderCommand.NotifyCanExecuteChanged();
            ExportFromTiaCommand.NotifyCanExecuteChanged();
            MarkExclusiveCommand.NotifyCanExecuteChanged();
        }
    }
}
