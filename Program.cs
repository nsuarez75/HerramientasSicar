using Avalonia;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace HerramientasSICAR
{
    internal static class Program
    {
        private static string _rutaApi;

        /// <summary>Hay TIA Portal V19 con Openness instalado en el equipo.</summary>
        public static bool HayTia => _rutaApi != null;

        [STAThread]
        public static void Main(string[] args)
        {
            // Siemens.Engineering no se copia junto al programa: se carga desde la instalación de TIA V19.
            // Tiene que estar antes de tocar cualquier clase que use Openness.
            _rutaApi = RutaApi();
            AppDomain.CurrentDomain.AssemblyResolve += ResolverOpenness;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Configuración de Avalonia; la usa también el diseñador visual.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        private static Assembly ResolverOpenness(object sender, ResolveEventArgs args)
        {
            string nombre = new AssemblyName(args.Name).Name;
            if (_rutaApi == null || !nombre.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase))
                return null;

            string ruta = Path.Combine(Path.GetDirectoryName(_rutaApi), nombre + ".dll");
            return File.Exists(ruta) ? Assembly.LoadFrom(ruta) : null;
        }

        /// <summary>
        /// Siemens.Engineering.dll de la API V19, según el registro de Openness; si no está registrada,
        /// la de una instalación estándar. Null si no existe.
        /// </summary>
        private static string RutaApi()
        {
            using (var maquina = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var clave = maquina.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\19.0\PublicAPI\V19"))
            {
                if (clave?.GetValue("Siemens.Engineering") is string ruta && File.Exists(ruta))
                    return ruta;
            }
            const string estandar = @"C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19\Siemens.Engineering.dll";
            return File.Exists(estandar) ? estandar : null;
        }
    }
}
