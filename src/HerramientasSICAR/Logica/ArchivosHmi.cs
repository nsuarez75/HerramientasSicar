using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logica
{
    /// <summary>Un XML de HMI a importar, con las carpetas donde debe quedar dentro de su rama.</summary>
    public class ArchivoHmi
    {
        public ArchivoHmi(string ruta, HmiBranch rama, IReadOnlyList<string> grupos)
        {
            Ruta = ruta;
            Rama = rama;
            Grupos = grupos;
        }

        public string Ruta { get; }
        public HmiBranch Rama { get; }
        public IReadOnlyList<string> Grupos { get; }
    }

    /// <summary>
    /// Prepara los XML que se importan en una HMI. Las reglas de las ramas (nombres, carpetas, tipo de cada XML) están
    /// en <see cref="HmiRules"/> de TIA-Connector. No depende de TIA Portal.
    /// </summary>
    public static class ArchivosHmi
    {
        /// <summary>
        /// Prepara los XML de una carpeta (y sus subcarpetas) para importarlos en una HMI.
        /// Si la ruta pasa por la carpeta de su rama (como la deja la exportación), cuenta lo que hay después;
        /// si no, toda la ruta. Los archivos que no son de HMI se devuelven aparte.
        /// </summary>
        public static List<ArchivoHmi> PrepararCarpeta(string carpeta, out List<string> descartados)
        {
            var rutas = Directory.GetFiles(carpeta, "*.xml", SearchOption.AllDirectories);
            return Separar(rutas, (ruta, rama) => HmiRules.FoldersInsideBranch(rama, FileRules.RelativeFolders(carpeta, ruta)), out descartados);
        }

        /// <summary>Prepara archivos sueltos: cada uno va al destino seleccionado o a la raíz de su rama.</summary>
        public static List<ArchivoHmi> PrepararArchivos(IEnumerable<string> rutas, out List<string> descartados)
        {
            return Separar(rutas, (ruta, rama) => new string[0], out descartados);
        }

        /// <summary>
        /// Carpetas de la rama donde queda el archivo. Si lo seleccionado en el árbol está en su misma rama,
        /// cuelga de ahí; si no, de la raíz de su rama. Las ramas sin carpetas no tienen.
        /// </summary>
        public static IReadOnlyList<string> CarpetasDestino(HmiBranch? ramaSeleccionada, IReadOnlyList<string> gruposSeleccionados, ArchivoHmi archivo)
        {
            if (!HmiRules.HasFolders(archivo.Rama))
                return new string[0];
            return ramaSeleccionada == archivo.Rama ? gruposSeleccionados.Concat(archivo.Grupos).ToList() : archivo.Grupos;
        }

        private static List<ArchivoHmi> Separar(IEnumerable<string> rutas, Func<string, HmiBranch, IReadOnlyList<string>> grupos,
            out List<string> descartados)
        {
            var archivos = new List<ArchivoHmi>();
            descartados = new List<string>();
            foreach (string ruta in rutas)
            {
                var rama = HmiRules.DetectBranch(ruta);
                if (rama == null)
                    descartados.Add(ruta);
                else
                    archivos.Add(new ArchivoHmi(ruta, rama.Value, grupos(ruta, rama.Value)));
            }
            return archivos
                .OrderBy(a => a.Rama)
                .ThenBy(a => string.Join("\\", a.Grupos), StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => Path.GetFileName(a.Ruta), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
