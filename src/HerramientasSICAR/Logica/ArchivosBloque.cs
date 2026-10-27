using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace HerramientasSICAR.Logica
{
    /// <summary>
    /// Tipo de bloque que contiene un XML exportado con Openness, según su primer elemento "SW.Blocks.*".
    /// El orden del enum es el orden de importación: primero lo que otros bloques usan.
    /// </summary>
    public enum TipoBloqueXml
    {
        FC,
        FB,
        GlobalDB,
        ArrayDB,
        InstanceDB,
        OB,
        Desconocido
    }

    /// <summary>Un XML a importar, con la ruta de grupos (carpetas) donde debe quedar dentro del grupo destino.</summary>
    public class ArchivoImportar
    {
        public ArchivoImportar(string ruta, TipoBloqueXml tipo, IReadOnlyList<string> grupos)
        {
            Ruta = ruta;
            Tipo = tipo;
            Grupos = grupos;
        }

        public string Ruta { get; }
        public TipoBloqueXml Tipo { get; }
        public IReadOnlyList<string> Grupos { get; }
    }

    /// <summary>
    /// Reglas de nombres de archivo y de orden de importación. No depende de TIA Portal.
    /// </summary>
    public static class ArchivosBloque
    {
        /// <summary>Lee solo el principio del XML y devuelve el tipo de bloque que contiene.</summary>
        public static TipoBloqueXml DetectarTipo(string rutaXml)
        {
            try
            {
                var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true };
                using (var lector = XmlReader.Create(rutaXml, ajustes))
                {
                    while (lector.Read())
                    {
                        if (lector.NodeType != XmlNodeType.Element || !lector.LocalName.StartsWith("SW.", StringComparison.Ordinal))
                            continue;

                        switch (lector.LocalName)
                        {
                            case "SW.Blocks.FC": return TipoBloqueXml.FC;
                            case "SW.Blocks.FB": return TipoBloqueXml.FB;
                            case "SW.Blocks.GlobalDB": return TipoBloqueXml.GlobalDB;
                            case "SW.Blocks.ArrayDB": return TipoBloqueXml.ArrayDB;
                            case "SW.Blocks.InstanceDB": return TipoBloqueXml.InstanceDB;
                            case "SW.Blocks.OB": return TipoBloqueXml.OB;
                            default: return TipoBloqueXml.Desconocido;
                        }
                    }
                }
            }
            catch (XmlException)
            {
            }
            return TipoBloqueXml.Desconocido;
        }

        /// <summary>
        /// Prepara los XML de una carpeta (y sus subcarpetas) para importarlos.
        /// Cada subcarpeta se convierte en un grupo; los archivos que no son bloques se devuelven aparte.
        /// </summary>
        public static List<ArchivoImportar> PrepararCarpeta(string carpeta, out List<string> descartados)
        {
            var archivos = Directory.GetFiles(carpeta, "*.xml", SearchOption.AllDirectories)
                .Select(ruta => new ArchivoImportar(ruta, DetectarTipo(ruta), GruposRelativos(carpeta, ruta)));
            return Separar(archivos, out descartados);
        }

        /// <summary>Prepara archivos sueltos: todos van directamente al grupo destino.</summary>
        public static List<ArchivoImportar> PrepararArchivos(IEnumerable<string> rutas, out List<string> descartados)
        {
            var archivos = rutas.Select(ruta => new ArchivoImportar(ruta, DetectarTipo(ruta), new string[0]));
            return Separar(archivos, out descartados);
        }

        /// <summary>Orden de importación: por tipo (FC, FB, DB, DB de instancia, OB) y luego por carpeta y nombre.</summary>
        public static List<ArchivoImportar> Ordenar(IEnumerable<ArchivoImportar> archivos)
        {
            return archivos
                .OrderBy(a => a.Tipo)
                .ThenBy(a => string.Join("\\", a.Grupos), StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => Path.GetFileName(a.Ruta), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Carpetas entre la carpeta base y el archivo, que se recrean como grupos en TIA.</summary>
        public static IReadOnlyList<string> GruposRelativos(string carpetaBase, string rutaArchivo)
        {
            string baseCompleta = Path.GetFullPath(carpetaBase).TrimEnd('\\', '/') + "\\";
            string carpetaArchivo = Path.GetDirectoryName(Path.GetFullPath(rutaArchivo)) + "\\";

            if (!carpetaArchivo.StartsWith(baseCompleta, StringComparison.OrdinalIgnoreCase))
                return new string[0];

            return carpetaArchivo.Substring(baseCompleta.Length)
                .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Si la ruta del archivo pasa del límite de Windows (y de TIA): 259 caracteres el archivo, 247 su carpeta.
        /// Las carpetas de una HMI suelen ser profundas y con nombres largos.
        /// </summary>
        public static bool RutaDemasiadoLarga(string rutaArchivo)
        {
            int barra = rutaArchivo.LastIndexOfAny(new[] { '\\', '/' });
            return rutaArchivo.Length >= 260 || barra >= 248;
        }

        /// <summary>Cambia por '_' los caracteres que Windows no admite en nombres de archivo o carpeta.</summary>
        public static string NombreSeguro(string nombre)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = new string(nombre.Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim();
            return limpio.Length == 0 ? "_" : limpio;
        }

        private static List<ArchivoImportar> Separar(IEnumerable<ArchivoImportar> archivos, out List<string> descartados)
        {
            var lista = archivos.ToList();
            descartados = lista.Where(a => a.Tipo == TipoBloqueXml.Desconocido).Select(a => a.Ruta).ToList();
            return Ordenar(lista.Where(a => a.Tipo != TipoBloqueXml.Desconocido));
        }
    }
}
