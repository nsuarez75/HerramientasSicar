using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HerramientasSICAR.Views
{
    /// <summary>
    /// Diálogos sencillos (mensaje, acerca de y confirmar), montados en código porque todos tienen la misma forma:
    /// título, contenido y botones abajo a la derecha.
    /// </summary>
    public sealed class DialogWindow : Window
    {
        private readonly StackPanel _buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

        private DialogWindow(Window dueño, string title, Control body, double width)
        {
            Title = title;
            Icon = dueño.Icon;
            Width = width;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 720;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var header = new TextBlock { Text = title, Classes = { "h2" }, TextWrapping = TextWrapping.Wrap };
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20) };
            header.Margin = new Thickness(0, 0, 0, 14);
            _buttons.Margin = new Thickness(0, 14, 0, 0);
            grid.Children.Add(header);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
            Grid.SetRow(_buttons, 2);
            grid.Children.Add(_buttons);
            Content = grid;
        }

        private void AñAddButton(string text, Action onClick, bool main = false, bool cancel = false)
        {
            var button = new Button { Content = text, IsDefault = main, IsCancel = cancel, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (main)
                button.Classes.Add("accent");
            button.Click += (_, __) => onClick();
            _buttons.Children.Add(button);
        }

        private static TextBlock Text(string text) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };

        public static async Task MessageNetwork(Window dueño, string title, string text)
        {
            var dialog = new DialogWindow(dueño, title, Text(text), 480);
            dialog.AñAddButton(L.T("Aceptar"), dialog.Close, main: true, cancel: true);
            await dialog.ShowDialog(dueño);
        }

        public static async Task About(Window dueño, string title, string text, string link)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(Text(text));
            panel.Children.Add(new HyperlinkButton { Content = link, NavigateUri = new Uri(link), Padding = new Thickness(0) });
            var dialog = new DialogWindow(dueño, title, panel, 480);
            dialog.AñAddButton(L.T("Aceptar"), dialog.Close, main: true, cancel: true);
            await dialog.ShowDialog(dueño);
        }

        public static async Task<bool> Confirm(Window dueño, string title, string text, string accept)
        {
            bool result = false;
            var dialog = new DialogWindow(dueño, title, new ScrollViewer { Content = Text(text), MaxHeight = 480 }, 520);
            dialog.AñAddButton(accept, () => { result = true; dialog.Close(); }, main: true);
            dialog.AñAddButton(L.T("Cancelar"), dialog.Close, cancel: true);
            await dialog.ShowDialog(dueño);
            return result;
        }

        public static async Task<IReadOnlyList<int>> ChooseMany(Window dueño, string title, string text, IReadOnlyList<string> options,
            IReadOnlyList<bool> checkedIndexes, string accept, string cancel)
        {
            IReadOnlyList<int> result = null;
            var checkBoxes = options.Select((option, i) => new CheckBox { Content = option, IsChecked = checkedIndexes[i] }).ToList();
            var list = new StackPanel { Spacing = 2 };
            foreach (var checkBox in checkBoxes)
                list.Children.Add(checkBox);

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(Text(text));
            panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 360 });

            var dialog = new DialogWindow(dueño, title, panel, 480);
            dialog.AñAddButton(accept, () =>
            {
                result = Enumerable.Range(0, checkBoxes.Count).Where(i => checkBoxes[i].IsChecked == true).ToList();
                dialog.Close();
            }, main: true);
            dialog.AñAddButton(cancel, dialog.Close, cancel: true);
            await dialog.ShowDialog(dueño);
            return result;
        }
    }
}
