using System.Collections.Generic;
using System.Threading.Tasks;

namespace HerramientasSICAR.ViewModels
{
    /// <summary>Tipo de archivo de un diálogo: nombre que se ve y patrones ("*.xlsx").</summary>
    public sealed class TipoArchivo
    {
        public TipoArchivo(string nombre, params string[] patrones)
        {
            Nombre = nombre;
            Patrones = patrones;
        }

        public string Nombre { get; }
        public IReadOnlyList<string> Patrones { get; }

        public static TipoArchivo Excel => new TipoArchivo(L.T("Libro de Excel (*.xlsx)"), "*.xlsx");
        public static TipoArchivo Xml => new TipoArchivo(L.T("Bloques exportados (*.xml)"), "*.xml");
    }

    /// <summary>Diálogos que los ViewModels piden a la ventana (la ventana los implementa).</summary>
    public interface IDialogos
    {
        Task Mensaje(string titulo, string texto);
        Task AcercaDe(string titulo, string texto, string enlace);
        Task<bool> Confirmar(string titulo, string texto, string aceptar);
        Task<string> ElegirCarpeta(string titulo, string inicial);

        /// <summary>Un archivo para abrir, o null si se cancela.</summary>
        Task<string> ElegirArchivo(string titulo, string inicial, params TipoArchivo[] tipos);

        /// <summary>Uno o varios archivos para abrir; vacío si se cancela.</summary>
        Task<IReadOnlyList<string>> ElegirArchivos(string titulo, string inicial, params TipoArchivo[] tipos);

        /// <summary>Dónde guardar un archivo nuevo, o null si se cancela.</summary>
        Task<string> GuardarArchivo(string titulo, string sugerido, TipoArchivo tipo);
    }
}
