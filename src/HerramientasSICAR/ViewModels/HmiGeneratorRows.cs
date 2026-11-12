using CommunityToolkit.Mvvm.ComponentModel;
using HerramientasSICAR.Logic.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Idioma que se edita en las cajas de texto del Generador HMI.</summary>
    public enum EditingLanguage { Text, English, German }

    /// <summary>
    /// Caja de texto sobre un <see cref="TextoHmi"/>: según el idioma que se edita, escribe en Texto, Ingles o Aleman.
    /// En inglés y alemán, el texto de marca de agua es el que saldría (el de Texto).
    /// </summary>
    public sealed class EditableText : ObservableObject
    {
        private readonly HmiText _text;
        private readonly HmiGeneratorViewModel _vm;
        private readonly Func<string> _standard;

        public EditableText(HmiText text, HmiGeneratorViewModel vm, Func<string> standard = null)
        {
            _text = text;
            _vm = vm;
            _standard = standard;
        }

        public string Value
        {
            get
            {
                switch (_vm.Language)
                {
                    case EditingLanguage.English: return _text.English;
                    case EditingLanguage.German: return _text.German;
                    default: return _text.Text;
                }
            }
            set
            {
                value = value ?? "";
                if (value == Value)
                    return;
                switch (_vm.Language)
                {
                    case EditingLanguage.English: _text.English = value; break;
                    case EditingLanguage.German: _text.German = value; break;
                    default: _text.Text = value; break;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(Placeholder));
                _vm.MarkChanged();
            }
        }

        /// <summary>Lo que sale si la caja se deja vacía.</summary>
        public string Placeholder =>
            _vm.Language != EditingLanguage.Text && !string.IsNullOrWhiteSpace(_text.Text) ? _text.Text : _standard?.Invoke() ?? "";

        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Placeholder));
        }

        // Solo la marca de agua: no toca el valor de una caja en la que se está escribiendo.
        public void RefreshCheck() => OnPropertyChanged(nameof(Placeholder));
    }

    /// <summary>Una HMI en la lista de HMIs del proyecto.</summary>
    public sealed class EditableHmi : ObservableObject
    {
        private readonly HmiGeneratorViewModel _vm;

        public EditableHmi(HmiMenus hmi, HmiGeneratorViewModel vm)
        {
            Hmi = hmi;
            _vm = vm;
        }

        public HmiMenus Hmi { get; }

        public string Name
        {
            get => Hmi.Name;
            set
            {
                if (value == Hmi.Name)
                    return;
                Hmi.Name = value ?? "";
                OnPropertyChanged();
                _vm.MarkChanged();
            }
        }

        public decimal? Number
        {
            get => Hmi.Number;
            set
            {
                int number = (int)(value ?? 1);
                if (number == Hmi.Number)
                    return;
                Hmi.Number = number;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Idb));
                _vm.MarkChanged();
            }
        }

        public string Idb => Hmi.IdbName;
    }

    /// <summary>Nodo del árbol de «Pantallas»: un grupo del menú (se pliega) o un subgrupo (se elige).</summary>
    public sealed partial class MenuNode : ObservableObject
    {
        private MenuNode(string name, MenuSubgroup subgroup, IReadOnlyList<MenuNode> children)
        {
            Name = name;
            Subgroup = subgroup;
            Children = children;
        }

        public static MenuNode FromGroup(MenuGroup group, IEnumerable<MenuSubgroup> subgroups) =>
            new MenuNode(group.Name, null, subgroups.Select(s => new MenuNode(s.Name, s, new MenuNode[0])).ToList());

        public string Name { get; }

        /// <summary>Null en un grupo.</summary>
        public MenuSubgroup Subgroup { get; }

        public IReadOnlyList<MenuNode> Children { get; }
        public bool IsGroup => Subgroup == null;

        [ObservableProperty]
        private bool _expanded = true;
    }

    /// <summary>Casilla «se ve en esta HMI» de una pantalla.</summary>
    public sealed class CheckBoxVisible : ObservableObject
    {
        private readonly List<bool> _visibleScreens;
        private readonly int _index;
        private readonly HmiGeneratorViewModel _vm;

        public CheckBoxVisible(List<bool> visibleScreens, int index, HmiGeneratorViewModel vm)
        {
            _visibleScreens = visibleScreens;
            _index = index;
            _vm = vm;
        }

        /// <summary>False si el FC no tiene esta posición (la casilla no se ve).</summary>
        public bool Exists => _visibleScreens != null;

        public bool Visible
        {
            get => Exists && _visibleScreens[_index];
            set
            {
                if (!Exists || value == _visibleScreens[_index])
                    return;
                _visibleScreens[_index] = value;
                OnPropertyChanged();
                _vm.MarkChanged();
            }
        }

        public void Refresh() => OnPropertyChanged(nameof(Visible));
    }

    /// <summary>Una pantalla (posición) del subgrupo elegido: su nombre y una casilla por HMI.</summary>
    public sealed class ScreenRow
    {
        public ScreenRow(int position, EditableText name, IReadOnlyList<CheckBoxVisible> checkBoxes)
        {
            Position = position;
            Name = name;
            CheckBoxes = checkBoxes;
        }

        public int Position { get; }
        public EditableText Name { get; }
        public bool HasName => Name != null;
        public IReadOnlyList<CheckBoxVisible> CheckBoxes { get; }
    }

    /// <summary>Una de las 24 pantallas de funciones de usuario.</summary>
    public sealed class FunctionScreen : ObservableObject
    {
        private readonly MenuProject _project;

        public FunctionScreen(int number, MenuProject project, IReadOnlyList<FunctionRow> functions)
        {
            Number = number;
            _project = project;
            Functions = functions;
        }

        public int Number { get; }
        public IReadOnlyList<FunctionRow> Functions { get; }

        public string Title
        {
            get
            {
                string name = _project.Name(MenuCatalog.UserFunctions, Number).Text;
                return string.IsNullOrWhiteSpace(name) ? Number.ToString() : Number + " · " + name;
            }
        }

        public int Configured => Functions.Count(f => !f.Function.IsEmptyFunction);

        public void Refresh()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Configured));
        }
    }

    /// <summary>Algo generado que se puede importar en TIA: el FC o una lista de textos.</summary>
    public sealed partial class ImportComponent : ObservableObject
    {
        public ImportComponent(string name)
        {
            Name = name;
        }

        public string Name { get; }

        [ObservableProperty]
        private bool _checked = true;
    }

    /// <summary>Grupo de componentes a importar, con «todos» y «ninguno».</summary>
    public sealed class ImportGroup
    {
        public ImportGroup(string title, IEnumerable<string> names)
        {
            Title = title;
            Components = names.Select(n => new ImportComponent(n)).ToList();
            AllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => Check(true));
            NoneCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => Check(false));
        }

        public string Title { get; }
        public IReadOnlyList<ImportComponent> Components { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand AllCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand NoneCommand { get; }

        private void Check(bool isChecked)
        {
            foreach (var component in Components)
                component.Checked = isChecked;
        }
    }

    /// <summary>Una HMI del proyecto conectado, como destino de las listas de textos.</summary>
    public sealed partial class HmiTarget : ObservableObject
    {
        public HmiTarget(string name, string displayName, bool isChecked)
        {
            Name = name;
            DisplayName = displayName;
            _checked = isChecked;
        }

        /// <summary>Nombre de la HMI en TIA: es con el que se importa.</summary>
        public string Name { get; }

        /// <summary>Nombre que se enseña (en un IPC, el de la estación).</summary>
        public string DisplayName { get; }

        [ObservableProperty]
        private bool _checked;
    }

    /// <summary>Un dataset de Production data (una posición del menú) con los textos de sus filas.</summary>
    public sealed class ProductionDataset : ObservableObject
    {
        public ProductionDataset(ProductionDataKind kind, int dataset, MenuProject project, HmiGeneratorViewModel vm)
        {
            Kind = kind;
            Dataset = dataset;
            Texts = project.Dataset(kind, dataset);
            Rows = Texts.Select((t, i) => new DatasetRow(i + 1, new EditableText(t, vm, () => Standard(vm, i + 1)))).ToList();
        }

        public ProductionDataKind Kind { get; }
        public int Dataset { get; }
        public IReadOnlyList<HmiText> Texts { get; }
        public IReadOnlyList<DatasetRow> Rows { get; }

        public string Title => Kind.Subgroup.Name + " · " + Dataset;
        public string TargetList => Kind.ListName(Dataset);
        public int Configured => Texts.Count(t => !t.IsEmpty);

        // Si el dataset está entero vacío salen los textos estándar; si no, lo vacío sale vacío.
        private string Standard(HmiGeneratorViewModel vm, int row)
        {
            if (Texts.Any(t => !t.IsEmpty))
                return "";
            int language = vm.Language == EditingLanguage.English ? 1 : vm.Language == EditingLanguage.German ? 2 : 0;
            return Kind.StandardText(language, Dataset, row);
        }

        public void Refresh() => OnPropertyChanged(nameof(Configured));
    }

    public sealed class DatasetRow
    {
        public DatasetRow(int number, EditableText text)
        {
            Number = number;
            Text = text;
        }

        public int Number { get; }
        public EditableText Text { get; }
    }

    /// <summary>Una función de usuario con sus cinco textos.</summary>
    public sealed class FunctionRow
    {
        public FunctionRow(int number, UserFunction function, HmiGeneratorViewModel vm, int screen)
        {
            Number = number;
            Function = function;
            Name = new EditableText(function.Name, vm, () => Standard(screen, "Name"));
            LeftAction = new EditableText(function.LeftAction, vm, () => Standard(screen, "Action left"));
            RightAction = new EditableText(function.RightAction, vm, () => Standard(screen, "Action right"));
            LeftFeedback = new EditableText(function.LeftFeedback, vm, () => Standard(screen, "Feed back left"));
            RightFeedback = new EditableText(function.RightFeedback, vm, () => Standard(screen, "Feed back right"));
        }

        // Si la función está entera vacía salen los textos estándar; si no, lo vacío sale vacío.
        private string Standard(int screen, string field) => Function.IsEmptyFunction ? $"<<S{screen} L{Number} {field}>>" : "";

        public int Number { get; }
        public UserFunction Function { get; }
        public EditableText Name { get; }
        public EditableText LeftAction { get; }
        public EditableText RightAction { get; }
        public EditableText LeftFeedback { get; }
        public EditableText RightFeedback { get; }

        public IEnumerable<EditableText> Texts => new[] { Name, LeftAction, RightAction, LeftFeedback, RightFeedback };
    }
}
