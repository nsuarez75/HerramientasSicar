using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using HerramientasSICAR.ViewModels;
using HerramientasSICAR.Views;

namespace HerramientasSICAR
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime escritorio)
            {
                var ajustes = Ajustes.Cargar();
                L.Idioma = ajustes.Idioma != null ? L.Normalizar(ajustes.Idioma) : L.IdiomaDelSistema();
                AplicarTema(ajustes.Tema);

                var principal = new MainViewModel(ajustes);
                var ventana = new MainWindow();
                ventana.Enlazar(principal);
                escritorio.MainWindow = ventana;

                principal.TemaCambiado += () => AplicarTema(ajustes.Tema);
                principal.IdiomaCambiado += () =>
                {
                    // Los textos se traducen al cargar las vistas: con otro idioma se monta una ventana nueva.
                    var vieja = escritorio.MainWindow;
                    var nueva = new MainWindow { Width = vieja.Width, Height = vieja.Height, Position = vieja.Position, WindowState = vieja.WindowState };
                    nueva.Enlazar(principal);
                    escritorio.MainWindow = nueva;
                    nueva.Show();
                    vieja.Close();
                };
                ventana.Opened += async (_, __) =>
                {
                    if (Program.HayTia)
                        await principal.BuscarProyectosCommand.ExecuteAsync(null);
                    else
                        principal.Contexto.MostrarEstado(L.T("No se encuentra TIA Portal V19 con Openness en este equipo. Las herramientas con archivos sí funcionan."), error: true);
                    await principal.ComprobarActualizaciones();
                };
                escritorio.Exit += (_, __) => principal.Dispose();
            }
            base.OnFrameworkInitializationCompleted();
        }

        private static void AplicarTema(string tema)
        {
            if (Current == null)
                return;
            Current.RequestedThemeVariant =
                tema == Ajustes.TemaClaro ? ThemeVariant.Light :
                tema == Ajustes.TemaOscuro ? ThemeVariant.Dark :
                ThemeVariant.Default;
        }
    }
}
