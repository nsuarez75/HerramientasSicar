using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HerramientasSICAR.Helpers
{
    public static class TiaVersionResolver
    {
        public static string SiemensEngineeringPath = "";
        public static string SiemensHmiPath = "";
        public static string TiaVersionDetected = "";

        /// <summary>
        /// Returns the Openness version based on project extension.
        /// </summary>
        private static string GetVersionFromProject(string proyecto)
        {
            string ext = Path.GetExtension(proyecto);
            if (ext == null)
                return null;

            ext = ext.ToLower();

            if (ext == ".ap19")
                return "19.0";

            if (ext == ".ap18")
                return "18.0";

            if (ext == ".ap17")
                return "17.0";

            if (ext == ".ap16")
                return "16.0";

            if (ext == ".ap15_1")
                return "15.1";

            return null;
        }

        /// <summary>
        /// Detects the required TIA version based on project (.apXX)
        /// </summary>
        public static bool DetectarDesdeProyecto(string rutaProyecto)
        {
            string versionNecesaria = GetVersionFromProject(rutaProyecto);
            if (versionNecesaria == null)
                return false;

            TiaVersionDetected = versionNecesaria;

            // Read installed versions from registry
            List<string> instaladas = RegistryReader.GetVersions();
            if (!instaladas.Contains(versionNecesaria))
                return false;

            // Get assemblies for the required version
            List<string> assemblies = RegistryReader.GetAssemblies(versionNecesaria);
            if (assemblies == null || assemblies.Count == 0)
                return false;

            // Last one (like .NET Core ^1)
            string ultimoAssembly = assemblies[assemblies.Count - 1];

            RegistryReader.GetAssemblyPath(versionNecesaria, ultimoAssembly,
                out SiemensEngineeringPath, out SiemensHmiPath);

            return File.Exists(SiemensEngineeringPath);
        }

        /// <summary>
        /// Assembly Resolver used if bindings fail.
        /// </summary>
        public static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            try
            {
                AssemblyName asm = new AssemblyName(args.Name);

                if (asm.Name.Equals("Siemens.Engineering", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(SiemensEngineeringPath))
                {
                    return Assembly.LoadFrom(SiemensEngineeringPath);
                }

                if (asm.Name.Equals("Siemens.Engineering.Hmi", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(SiemensHmiPath))
                {
                    return Assembly.LoadFrom(SiemensHmiPath);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
