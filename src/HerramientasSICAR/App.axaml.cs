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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var settings = Settings.Load();
                L.Language = settings.Language != null ? L.Normalize(settings.Language) : L.SystemLanguage();
                ApplyTheme(settings.Theme);

                var main = new MainViewModel(settings);
                var window = new MainWindow();
                window.Bind(main);
                desktop.MainWindow = window;

                main.ThemeChanged += () => ApplyTheme(settings.Theme);
                main.LanguageChanged += () =>
                {
                    // Los textos se traducen al cargar las vistas: con otro idioma se monta una ventana nueva.
                    var oldWindow = desktop.MainWindow;
                    var newer = new MainWindow { Width = oldWindow.Width, Height = oldWindow.Height, Position = oldWindow.Position, WindowState = oldWindow.WindowState };
                    newer.Bind(main);
                    desktop.MainWindow = newer;
                    newer.Show();
                    oldWindow.Close();
                };
                window.Opened += async (_, __) =>
                {
                    if (Program.HasTia)
                        await main.FindProjectsCommand.ExecuteAsync(null);
                    else
                        main.ToolContext.ShowStatus(L.T("No se encuentra TIA Portal V19 con Openness en este equipo. Las herramientas con archivos sí funcionan."), error: true);
                    await main.CheckForUpdates();
                };
                desktop.Exit += (_, __) => main.Dispose();
            }
            base.OnFrameworkInitializationCompleted();
        }

        private static void ApplyTheme(string theme)
        {
            if (Current == null)
                return;
            Current.RequestedThemeVariant =
                theme == Settings.LightTheme ? ThemeVariant.Light :
                theme == Settings.DarkTheme ? ThemeVariant.Dark :
                ThemeVariant.Default;
        }
    }
}
