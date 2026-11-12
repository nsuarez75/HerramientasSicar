using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logic
{
    /// <summary>Un XML de HMI a importar, con las carpetas donde debe quedar dentro de su rama.</summary>
    public class HmiFile
    {
        public HmiFile(string path, HmiBranch branch, IReadOnlyList<string> groups)
        {
            FilePath = path;
            Branch = branch;
            Groups = groups;
        }

        public string FilePath { get; }
        public HmiBranch Branch { get; }
        public IReadOnlyList<string> Groups { get; }
    }

    /// <summary>
    /// Prepara los XML que se importan en una HMI. Las reglas de las ramas (nombres, carpetas, tipo de cada XML) están
    /// en <see cref="HmiRules"/> de TIA-Connector. No depende de TIA Portal.
    /// </summary>
    public static class HmiFiles
    {
        /// <summary>
        /// Prepara los XML de una carpeta (y sus subcarpetas) para importarlos en una HMI.
        /// Si la ruta pasa por la carpeta de su rama (como la deja la exportación), cuenta lo que hay después;
        /// si no, toda la ruta. Los archivos que no son de HMI se devuelven aparte.
        /// </summary>
        public static List<HmiFile> PrepareFolder(string folder, out List<string> discarded)
        {
            var paths = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
            return Split(paths, (path, branch) => HmiRules.FoldersInsideBranch(branch, FileRules.RelativeFolders(folder, path)), out discarded);
        }

        /// <summary>Prepara archivos sueltos: cada uno va al destino seleccionado o a la raíz de su rama.</summary>
        public static List<HmiFile> PrepareFiles(IEnumerable<string> paths, out List<string> discarded)
        {
            return Split(paths, (path, branch) => new string[0], out discarded);
        }

        /// <summary>
        /// Carpetas de la rama donde queda el archivo. Si lo seleccionado en el árbol está en su misma rama,
        /// cuelga de ahí; si no, de la raíz de su rama. Las ramas sin carpetas no tienen.
        /// </summary>
        public static IReadOnlyList<string> TargetFolders(HmiBranch? selectedBranch, IReadOnlyList<string> selectedFolders, HmiFile file)
        {
            if (!HmiRules.HasFolders(file.Branch))
                return new string[0];
            return selectedBranch == file.Branch ? selectedFolders.Concat(file.Groups).ToList() : file.Groups;
        }

        private static List<HmiFile> Split(IEnumerable<string> paths, Func<string, HmiBranch, IReadOnlyList<string>> groups,
            out List<string> discarded)
        {
            var files = new List<HmiFile>();
            discarded = new List<string>();
            foreach (string path in paths)
            {
                var branch = HmiRules.DetectBranch(path);
                if (branch == null)
                    discarded.Add(path);
                else
                    files.Add(new HmiFile(path, branch.Value, groups(path, branch.Value)));
            }
            return files
                .OrderBy(a => a.Branch)
                .ThenBy(a => string.Join("\\", a.Groups), StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => Path.GetFileName(a.FilePath), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
