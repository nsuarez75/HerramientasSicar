using System.Collections.Generic;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Tipo de archivo de un diálogo: nombre que se ve y patrones ("*.xlsx").</summary>
    public sealed class FileKind
    {
        public FileKind(string name, params string[] patterns)
        {
            Name = name;
            Patterns = patterns;
        }

        public string Name { get; }
        public IReadOnlyList<string> Patterns { get; }

        public static FileKind Excel => new FileKind(L.T("Libro de Excel (*.xlsx)"), "*.xlsx");
        public static FileKind Xml => new FileKind(L.T("Bloques exportados (*.xml)"), "*.xml");
    }

    /// <summary>Diálogos que los ViewModels piden a la ventana (la ventana los implementa).</summary>
    public interface IDialogs
    {
        Task MessageNetwork(string title, string text);
        Task About(string title, string text, string link);
        Task<bool> Confirm(string title, string text, string accept);
        Task<string> ChooseFolder(string title, string initial);

        /// <summary>Casillas para elegir varias opciones: las posiciones marcadas, o null si se cancela.</summary>
        Task<IReadOnlyList<int>> ChooseMany(string title, string text, IReadOnlyList<string> options, IReadOnlyList<bool> checkedIndexes,
            string accept, string cancel);

        /// <summary>Un archivo para abrir, o null si se cancela.</summary>
        Task<string> ChooseFile(string title, string initial, params FileKind[] types);

        /// <summary>Uno o varios archivos para abrir; vacío si se cancela.</summary>
        Task<IReadOnlyList<string>> ChooseFiles(string title, string initial, params FileKind[] types);

        /// <summary>Dónde guardar un archivo nuevo, o null si se cancela.</summary>
        Task<string> SaveFile(string title, string suggested, FileKind kind);
    }
}
