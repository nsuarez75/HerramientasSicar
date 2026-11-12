using Avalonia;
using Avalonia.Controls;

namespace HerramientasSICAR.Views
{
    public partial class SyncCheckerView : UserControl
    {
        public SyncCheckerView()
        {
            InitializeComponent();
            // La decisión sobre el par y las decisiones guardadas se pueden plegar y su alto se cambia arrastrando la
            // separación: así, con muchas señales, no quitan sitio a la lista de pares.
            Foldable(PairArea, 2, DecisionPanel, DecisionCard, DecisionSplitter, 220);
            Foldable(Body, 2, SavedPanel, SavedPanel, SavedSplitter, 160);
        }

        // La fila del panel tiene alto fijo (el que se deja al arrastrar) mientras se ve desplegado; plegado u oculto,
        // solo ocupa lo que necesita y la separación no se puede arrastrar.
        private static void Foldable(Grid grid, int row, Expander panel, Control shown, GridSplitter splitter, double height)
        {
            var definition = grid.RowDefinitions[row];
            double last = height;

            void Update()
            {
                bool open = panel.IsExpanded && shown.IsVisible;
                if (!open && definition.Height.IsAbsolute && definition.ActualHeight > 0)
                    last = definition.ActualHeight;
                definition.Height = open ? new GridLength(last) : GridLength.Auto;
                splitter.IsEnabled = open;
            }

            panel.PropertyChanged += (_, e) =>
            {
                if (e.Property == Expander.IsExpandedProperty)
                    Update();
            };
            shown.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsVisibleProperty)
                    Update();
            };
            Update();
        }
    }
}
