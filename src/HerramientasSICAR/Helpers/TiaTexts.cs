using System;
using System.Collections.Generic;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Helpers
{
    /// <summary>
    /// Textos de la app para lo que devuelve TIA-Connector. El puente contesta con códigos (etapa, motivo del fallo,
    /// error) y textos de TIA sin traducir; aquí se componen los mensajes en el idioma de la app.
    /// </summary>
    public static class TiaTexts
    {
        /// <summary>Por dónde va una operación, para la barra de estado.</summary>
        public static string Progress(ProgressInfo progress)
        {
            string item = Item(progress);
            bool counted = progress.Total > 0 && progress.Current > 0;
            switch (progress.Stage)
            {
                case ProgressStage.Connecting:
                    return L.T("Conectando… (si TIA pide permiso para esta aplicación, acéptalo)");
                case ProgressStage.OpeningProject:
                    return L.T("Abriendo {0} en TIA Portal…", item);
                case ProgressStage.ReadingTree:
                    return counted
                        ? L.T("Leyendo el árbol del proyecto: {0} ({1}/{2})…", item, progress.Current, progress.Total)
                        : L.T("Leyendo el árbol del proyecto…");
                case ProgressStage.Exporting:
                    return counted ? L.T("Exportando {0}/{1}: {2}", progress.Current, progress.Total, item) : L.T("Exportando…");
                case ProgressStage.Importing:
                    return counted ? L.T("Importando {0}/{1}: {2}", progress.Current, progress.Total, item) : L.T("Importando…");
                case ProgressStage.Compiling:
                    return string.IsNullOrEmpty(item) ? L.T("Compilando el proyecto…") : L.T("Compilando {0}…", item);
                default:
                    return item;
            }
        }

        // Al leer una HMI, el puente manda «HMI/Rama» con el nombre de la rama en inglés: se pone el de la app.
        private static string Item(ProgressInfo progress)
        {
            string item = progress.Item ?? "";
            int bar = item.LastIndexOf('/');
            if (progress.Stage == ProgressStage.ReadingTree && bar > 0
                && Enum.TryParse(item.Substring(bar + 1), out HmiBranch branch))
                return item.Substring(0, bar) + " › " + L.T(HmiRules.FolderName(branch));
            return item;
        }

        /// <summary>
        /// Un texto «elemento: motivo» por cada fallo del resultado y, si TIA se cerró a mitad, cuántos quedaron sin
        /// hacer. <paramref name="importando"/>: un bloque que no compila tras importarlo queda importado igualmente.
        /// </summary>
        public static List<string> Failures(BatchResult result, bool importing = false)
        {
            var failures = result.Failures.Select(f => $"{f.Item}: {Reason(f, importing)}").ToList();
            if (result.Stopped)
                failures.Add(L.T("TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer.", result.Remaining));
            return failures;
        }

        private static string Reason(ItemFailure failure, bool importing)
        {
            switch (failure.Code)
            {
                case FailureCode.Protected: return L.T("está protegido con contraseña (know-how)");
                case FailureCode.NotCompiled: return L.T("sin compilar");
                case FailureCode.DoesNotCompile:
                    return importing ? L.T("importado, pero no compila; revísalo en TIA") : L.T("no compila; revisa sus errores en TIA");
                case FailureCode.NotFound: return L.T("ya no existe en el proyecto");
                case FailureCode.PathTooLong: return L.T("la ruta es demasiado larga para Windows; usa una carpeta con una ruta más corta");
                case FailureCode.DefaultTagTable: return L.T("es la tabla de variables estándar de la HMI; no se sobrescribe");
                case FailureCode.Unsupported: return L.T("esta HMI no admite ese tipo de elemento");
                default: return string.IsNullOrWhiteSpace(failure.Detail) ? L.T("error de TIA sin detalle") : failure.Detail;
            }
        }

        /// <summary>Error de una petición entera.</summary>
        public static string Error(TiaConnectorException error)
        {
            switch (error.Code)
            {
                case ErrorCode.NoOpenness:
                    return L.T("No se encuentra TIA Portal V19 con Openness en este equipo.");
                case ErrorCode.NoPermission:
                    return L.T("TIA Portal no ha dejado entrar a la aplicación: acepta su aviso o comprueba que tu usuario está en el grupo «Siemens TIA Openness».");
                case ErrorCode.NotConnected:
                    return L.T("Conecta con un proyecto de TIA en la barra superior.");
                case ErrorCode.ProjectNotOpen:
                case ErrorCode.TiaClosed:
                    return L.T("El proyecto ya no está abierto en TIA Portal. Vuelve a conectar.");
                case ErrorCode.BridgeExited:
                    return L.T("Se ha cortado la conexión con TIA Portal. Vuelve a conectar.");
                default:
                    return L.T("Error: {0}", error.Message);
            }
        }
    }
}
