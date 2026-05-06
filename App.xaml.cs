using HerramientasSICAR.Services;
using HerramientasSICAR.ViewModels;
using HerramientasSICAR.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace HerramientasSICAR
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            // Set up assembly resolver for Siemens TIA Openness
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Get the assembly name
            string assemblyName = new AssemblyName(args.Name).Name;

            // Path to TIA Portal PublicAPI directory
            string tiaPortalPath = @"C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19";

            // Try to find the assembly in the TIA Portal directory
            string assemblyPath = Path.Combine(tiaPortalPath, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                try
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading assembly {assemblyName}: {ex.Message}");
                }
            }

            return null;
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Services - Lazy loaded to avoid startup issues
            services.AddSingleton<TiaOpennessService>(provider => new TiaOpennessService());
            services.AddSingleton<NavigationService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DiagExpectedViewModel>();
            services.AddTransient<NumeradorViewModel>();
            services.AddTransient<ComentadorViewModel>();
            services.AddTransient<RenombrarArrayViewModel>();

            // Views
            services.AddSingleton<MainWindow>(provider =>
            {
                var window = new MainWindow
                {
                    DataContext = provider.GetRequiredService<MainViewModel>()
                };
                return window;
            });
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
