using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TiaConnector.Contracts;

namespace HerramientasSICAR.Logic
{
    /// <summary>
    /// Tipo de bloque que contiene un XML exportado con Openness, según su primer elemento "SW.Blocks.*".
    /// El orden del enum es el orden de importación: primero lo que otros bloques usan.
    /// </summary>
    public enum XmlBlockKind
    {
        FC,
        FB,
        GlobalDB,
        ArrayDB,
        InstanceDB,
        OB,
        Unknown
    }

    /// <summary>Un XML a importar, con la ruta de grupos (carpetas) donde debe quedar dentro del grupo destino.</summary>
    public class BlockImportFile
    {
        public BlockImportFile(string path, XmlBlockKind kind, IReadOnlyList<string> groups)
        {
            FilePath = path;
            Kind = kind;
            Groups = groups;
        }

        public string FilePath { get; }
        public XmlBlockKind Kind { get; }
        public IReadOnlyList<string> Groups { get; }
    }

    /// <summary>
    /// Tipo y orden de importación de los XML de bloques. Las reglas de nombres y rutas están en <see cref="FileRules"/>
    /// de TIA-Connector. No depende de TIA Portal.
    /// </summary>
    public static class BlockFiles
    {
        /// <summary>Lee solo el principio del XML y devuelve el tipo de bloque que contiene.</summary>
        public static XmlBlockKind DetectKind(string xmlPath)
        {
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true };
                using (var reader = XmlReader.Create(xmlPath, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || !reader.LocalName.StartsWith("SW.", StringComparison.Ordinal))
                            continue;

                        switch (reader.LocalName)
                        {
                            case "SW.Blocks.FC": return XmlBlockKind.FC;
                            case "SW.Blocks.FB": return XmlBlockKind.FB;
                            case "SW.Blocks.GlobalDB": return XmlBlockKind.GlobalDB;
                            case "SW.Blocks.ArrayDB": return XmlBlockKind.ArrayDB;
                            case "SW.Blocks.InstanceDB": return XmlBlockKind.InstanceDB;
                            case "SW.Blocks.OB": return XmlBlockKind.OB;
                            default: return XmlBlockKind.Unknown;
                        }
                    }
                }
            }
            catch (XmlException)
            {
            }
            return XmlBlockKind.Unknown;
        }

        /// <summary>
        /// Prepara los XML de una carpeta (y sus subcarpetas) para importarlos.
        /// Cada subcarpeta se convierte en un grupo; los archivos que no son bloques se devuelven aparte.
        /// </summary>
        public static List<BlockImportFile> PrepareFolder(string folder, out List<string> discarded)
        {
            var files = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories)
                .Select(path => new BlockImportFile(path, DetectKind(path), FileRules.RelativeFolders(folder, path)));
            return Split(files, out discarded);
        }

        /// <summary>Prepara archivos sueltos: todos van directamente al grupo destino.</summary>
        public static List<BlockImportFile> PrepareFiles(IEnumerable<string> paths, out List<string> discarded)
        {
            var files = paths.Select(path => new BlockImportFile(path, DetectKind(path), new string[0]));
            return Split(files, out discarded);
        }

        /// <summary>Orden de importación: por tipo (FC, FB, DB, DB de instancia, OB) y luego por carpeta y nombre.</summary>
        public static List<BlockImportFile> Sort(IEnumerable<BlockImportFile> files)
        {
            return files
                .OrderBy(a => a.Kind)
                .ThenBy(a => string.Join("\\", a.Groups), StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => Path.GetFileName(a.FilePath), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<BlockImportFile> Split(IEnumerable<BlockImportFile> files, out List<string> discarded)
        {
            var list = files.ToList();
            discarded = list.Where(a => a.Kind == XmlBlockKind.Unknown).Select(a => a.FilePath).ToList();
            return Sort(list.Where(a => a.Kind != XmlBlockKind.Unknown));
        }
    }
}
