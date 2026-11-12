// Dibuja un escalón de ladder a partir de su disposición en rejilla: barra izquierda, instrucciones, cables y la
// bobina que alimenta. Con un estado de señales, cada instrucción sale en verde si conduce, en rojo si no y sin
// color si el estado no la decide; la bobina, igual. Al pulsar una instrucción se ejecuta ToggleCommand con su señal.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Sicar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace HerramientasSICAR.Controls
{
    public sealed class LadRungView : Control
    {
        private const double CellWidth = 184;
        private const double CellHeight = 62;
        private const double Rail = 14;
        private const double CoilWidth = 150;

        public static readonly StyledProperty<LadRung> RungProperty = AvaloniaProperty.Register<LadRungView, LadRung>(nameof(Rung));

        public static readonly StyledProperty<IReadOnlyDictionary<string, bool>> StateProperty =
            AvaloniaProperty.Register<LadRungView, IReadOnlyDictionary<string, bool>>(nameof(State));

        public static readonly StyledProperty<ICommand> ToggleCommandProperty =
            AvaloniaProperty.Register<LadRungView, ICommand>(nameof(ToggleCommand));

        static LadRungView()
        {
            AffectsMeasure<LadRungView>(RungProperty);
            AffectsRender<LadRungView>(RungProperty, StateProperty);
        }

        public LadRung Rung
        {
            get => GetValue(RungProperty);
            set => SetValue(RungProperty, value);
        }

        public IReadOnlyDictionary<string, bool> State
        {
            get => GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public ICommand ToggleCommand
        {
            get => GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var layout = Rung?.Layout;
            return layout == null
                ? default
                : new Size(Rail + layout.Columns * CellWidth + CoilWidth + Rail, layout.Rows * CellHeight + 2);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetPosition(this);
            var column = (int)Math.Floor((point.X - Rail) / CellWidth);
            var row = (int)Math.Floor(point.Y / CellHeight);
            var cell = Rung?.Layout.Cells.FirstOrDefault(c => c.Column == column && c.Row == row);
            if (cell != null && SyncState.Signal(cell.Leaf) is string signal && ToggleCommand?.CanExecute(signal) == true)
                ToggleCommand.Execute(signal);
        }

        public override void Render(DrawingContext context)
        {
            if (Rung == null)
                return;
            var layout = Rung.Layout;
            var text = Brush("App.Text");
            var muted = Brush("App.TextMuted");
            var line = new Pen(muted, 1.4);
            var endColumn = Math.Max(layout.Columns, 0);

            // Barra de alimentación y cable hasta la bobina.
            context.DrawLine(new Pen(text, 2), new Point(1, 0), new Point(1, layout.Rows * CellHeight));
            context.DrawLine(line, new Point(1, Y(0)), new Point(X(0), Y(0)));
            context.DrawLine(line, new Point(X(endColumn), Y(0)), new Point(X(endColumn) + 24, Y(0)));

            foreach (var wire in layout.Wires)
                context.DrawLine(line, new Point(X(wire.Column1), Y(wire.Row1)), new Point(X(wire.Column2), Y(wire.Row2)));
            foreach (var cell in layout.Cells)
                DrawCell(context, cell, line, text, muted);
            DrawCoil(context, Rung.Coil, X(endColumn) + 24, line, ValueBrush(Rung.Logic) ?? text);
        }

        private static double X(int column) => Rail + column * CellWidth;

        private static double Y(int row) => row * CellHeight + CellHeight * 0.62;

        // Verde si conduce con el estado, rojo si no; null si el estado no lo decide o no hay estado.
        private IBrush ValueBrush(LadExpression expression) =>
            State == null ? null : SyncState.Value(expression, State) switch
            {
                true => Brush("App.Good"),
                false => Brush("App.Bad"),
                _ => null,
            };

        private void DrawCell(DrawingContext context, LadCell cell, Pen line, IBrush text, IBrush muted)
        {
            var (x, y) = (X(cell.Column), Y(cell.Row));
            var value = ValueBrush(cell.Leaf);
            if (value != null)
            {
                var soft = SyncState.Value(cell.Leaf, State) == true ? "App.GoodSoft" : "App.BadSoft";
                context.FillRectangle(Brush(soft), new Rect(x + 2, cell.Row * CellHeight + 1, CellWidth - 4, CellHeight - 2), 6);
            }
            var symbol = value ?? text;
            var center = x + CellWidth / 2;
            switch (cell.Leaf)
            {
                case ContactExpression contact:
                    DrawContact(context, x, y, center, line, symbol, contact.Negated);
                    Label(context, OperandName(contact.Operand), center, cell.Row, text);
                    break;
                case EdgeContact edge:
                    DrawContact(context, x, y, center, line, symbol, false);
                    Text(context, edge.Rising ? "P" : "N", center - 3.5, y - 7, 10, muted, 8);
                    Label(context, OperandName(edge.Operand), center, cell.Row, text);
                    break;
                case Comparison comparison:
                    DrawBox(context, x, y, line, Comparison.Symbol(comparison.Operator), Value(comparison.Left), Value(comparison.Right), comparison.DataType, symbol, muted);
                    break;
                case BoxOutput box:
                    DrawBox(context, x, y, line, box.Box, box.Instance != null ? OperandName(box.Instance) : "", box.Pin.ToUpperInvariant(), "", symbol, muted);
                    break;
                default:
                    DrawBox(context, x, y, line, cell.Leaf.ToString() ?? "?", "", "", "", symbol, muted);
                    break;
            }
        }

        private static void DrawContact(DrawingContext context, double x, double y, double center, Pen line, IBrush brush, bool negated)
        {
            var pen = new Pen(brush, 1.8);
            context.DrawLine(line, new Point(x, y), new Point(center - 7, y));
            context.DrawLine(line, new Point(center + 7, y), new Point(x + CellWidth, y));
            context.DrawLine(pen, new Point(center - 7, y - 10), new Point(center - 7, y + 10));
            context.DrawLine(pen, new Point(center + 7, y - 10), new Point(center + 7, y + 10));
            if (negated)
                context.DrawLine(pen, new Point(center - 11, y + 10), new Point(center + 11, y - 10));
        }

        private void DrawBox(DrawingContext context, double x, double y, Pen line, string title, string top, string bottom, string type, IBrush brush, IBrush muted)
        {
            var box = new Rect(x + 8, y - 30, CellWidth - 16, 44);
            context.DrawLine(line, new Point(x, y), new Point(box.X, y));
            context.DrawLine(line, new Point(box.Right, y), new Point(x + CellWidth, y));
            context.DrawRectangle(Brush("App.Surface"), new Pen(brush, 1.2), box, 3, 3);
            Text(context, title, box.X + 5, box.Y + 1, 10.5, brush, box.Width - 10, FontWeight.SemiBold);
            if (type.Length > 0)
                Text(context, type, box.Right - 34, box.Y + 1, 9, muted, 30);
            Text(context, top, box.X + 5, box.Y + 15, 10, brush, box.Width - 10);
            Text(context, bottom, box.X + 5, box.Y + 28, 10, brush, box.Width - 10);
        }

        private void DrawCoil(DrawingContext context, string coil, double x, Pen line, IBrush brush)
        {
            var y = Y(0);
            var pen = new Pen(brush, 1.8);
            context.DrawEllipse(null, pen, new Point(x + 9, y), 9, 11);
            Text(context, coil, x + 24, y - 8, 11.5, brush, CoilWidth - 26, FontWeight.SemiBold);
        }

        private void Label(DrawingContext context, string name, double center, int row, IBrush brush)
        {
            var formatted = Format(name, 10.5, brush, CellWidth - 8, FontWeight.Normal);
            context.DrawText(formatted, new Point(center - formatted.Width / 2, row * CellHeight + 4));
        }

        private void Text(DrawingContext context, string value, double x, double y, double size, IBrush brush, double width, FontWeight weight = FontWeight.Normal) =>
            context.DrawText(Format(value, size, brush, width, weight), new Point(x, y));

        private static FormattedText Format(string value, double size, IBrush brush, double width, FontWeight weight) =>
            new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, weight: weight), size, brush)
            {
                MaxTextWidth = width,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };

        private IBrush Brush(string key) =>
            this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

        private static string OperandName(Operand operand) => operand.Scope == OperandScope.Global ? operand.Path : operand.Text;

        private static string Value(LadValue value) => value.Operand != null ? OperandName(value.Operand) : value.ToString();
    }
}
