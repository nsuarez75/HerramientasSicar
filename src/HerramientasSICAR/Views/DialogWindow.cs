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
        private readonly StackPanel _botones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

        private DialogWindow(Window dueño, string titulo, Control cuerpo, double ancho)
        {
            Title = titulo;
            Icon = dueño.Icon;
            Width = ancho;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 720;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var cabecera = new TextBlock { Text = titulo, Classes = { "h2" }, TextWrapping = TextWrapping.Wrap };
            var rejilla = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20) };
            cabecera.Margin = new Thickness(0, 0, 0, 14);
            _botones.Margin = new Thickness(0, 14, 0, 0);
            rejilla.Children.Add(cabecera);
            Grid.SetRow(cuerpo, 1);
            rejilla.Children.Add(cuerpo);
            Grid.SetRow(_botones, 2);
            rejilla.Children.Add(_botones);
            Content = rejilla;
        }

        private void AñadirBoton(string texto, Action alPulsar, bool principal = false, bool cancelar = false)
        {
            var boton = new Button { Content = texto, IsDefault = principal, IsCancel = cancelar, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (principal)
                boton.Classes.Add("accent");
            boton.Click += (_, __) => alPulsar();
            _botones.Children.Add(boton);
        }

        private static TextBlock Texto(string texto) => new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap };

        public static async Task Mensaje(Window dueño, string titulo, string texto)
        {
            var dialogo = new DialogWindow(dueño, titulo, Texto(texto), 480);
            dialogo.AñadirBoton(L.T("Aceptar"), dialogo.Close, principal: true, cancelar: true);
            await dialogo.ShowDialog(dueño);
        }

        public static async Task AcercaDe(Window dueño, string titulo, string texto, string enlace)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(Texto(texto));
            panel.Children.Add(new HyperlinkButton { Content = enlace, NavigateUri = new Uri(enlace), Padding = new Thickness(0) });
            var dialogo = new DialogWindow(dueño, titulo, panel, 480);
            dialogo.AñadirBoton(L.T("Aceptar"), dialogo.Close, principal: true, cancelar: true);
            await dialogo.ShowDialog(dueño);
        }

        public static async Task<bool> Confirmar(Window dueño, string titulo, string texto, string aceptar)
        {
            bool resultado = false;
            var dialogo = new DialogWindow(dueño, titulo, new ScrollViewer { Content = Texto(texto), MaxHeight = 480 }, 520);
            dialogo.AñadirBoton(aceptar, () => { resultado = true; dialogo.Close(); }, principal: true);
            dialogo.AñadirBoton(L.T("Cancelar"), dialogo.Close, cancelar: true);
            await dialogo.ShowDialog(dueño);
            return resultado;
        }

        public static async Task<IReadOnlyList<int>> ElegirVarios(Window dueño, string titulo, string texto, IReadOnlyList<string> opciones,
            IReadOnlyList<bool> marcadas, string aceptar, string cancelar)
        {
            IReadOnlyList<int> resultado = null;
            var casillas = opciones.Select((opcion, i) => new CheckBox { Content = opcion, IsChecked = marcadas[i] }).ToList();
            var lista = new StackPanel { Spacing = 2 };
            foreach (var casilla in casillas)
                lista.Children.Add(casilla);

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(Texto(texto));
            panel.Children.Add(new ScrollViewer { Content = lista, MaxHeight = 360 });

            var dialogo = new DialogWindow(dueño, titulo, panel, 480);
            dialogo.AñadirBoton(aceptar, () =>
            {
                resultado = Enumerable.Range(0, casillas.Count).Where(i => casillas[i].IsChecked == true).ToList();
                dialogo.Close();
            }, principal: true);
            dialogo.AñadirBoton(cancelar, dialogo.Close, cancelar: true);
            await dialogo.ShowDialog(dueño);
            return resultado;
        }
    }
}
