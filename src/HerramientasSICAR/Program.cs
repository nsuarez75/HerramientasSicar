using Avalonia;
using Microsoft.Win32;
using System;
using System.IO;

namespace HerramientasSICAR
{
    internal static class Program
    {
        /// <summary>
        /// Hay TIA Portal V19 con Openness instalado en el equipo. Solo sirve para avisar al arrancar: con TIA habla el
        /// puente de TIA-Connector, que carga Openness en su propio proceso.
        /// </summary>
        public static bool HayTia { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            HayTia = RutaApi() != null;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Configuración de Avalonia; la usa también el diseñador visual.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

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
