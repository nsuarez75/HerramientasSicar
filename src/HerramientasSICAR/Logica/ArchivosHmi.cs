using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace HerramientasSICAR.Logica
{
    /// <summary>
    /// Rama de una HMI WinCC clásica (Basic, Comfort, Advanced) que se puede exportar e importar con Openness.
    /// El orden del enum es el orden de importación: primero lo que usan las demás (las variables usan
    /// conexiones y ciclos; las pantallas usan variables, listas, scripts y plantillas; la vista general, pantallas).
    /// </summary>
    public enum RamaHmi
    {
        Conexiones,
        Ciclos,
        ListasTextos,
        ListasGraficos,
        Variables,
        Scripts,
        Plantillas,
        ElementosGlobales,
        PopUps,
        SlideIns,
        Pantallas,
        VistaGeneral
    }

    /// <summary>Un XML de HMI a importar, con las carpetas donde debe quedar dentro de su rama.</summary>
    public class ArchivoHmi
    {
        public ArchivoHmi(string ruta, RamaHmi rama, IReadOnlyList<string> grupos)
        {
            Ruta = ruta;
            Rama = rama;
            Grupos = grupos;
        }

        public string Ruta { get; }
        public RamaHmi Rama { get; }
        public IReadOnlyList<string> Grupos { get; }
    }

    /// <summary>
    /// Reglas de las ramas de una HMI: nombres, carpetas, tipo de cada XML y orden de importación. No depende de TIA Portal.
    /// </summary>
    public static class ArchivosHmi
    {
        /// <summary>Orden en que se ven las ramas en el árbol, como en TIA.</summary>
        public static readonly IReadOnlyList<RamaHmi> OrdenArbol = new[]
        {
            RamaHmi.Pantallas, RamaHmi.Plantillas, RamaHmi.PopUps, RamaHmi.SlideIns, RamaHmi.ElementosGlobales,
            RamaHmi.VistaGeneral, RamaHmi.Variables, RamaHmi.Conexiones, RamaHmi.Ciclos, RamaHmi.ListasTextos,
            RamaHmi.ListasGraficos, RamaHmi.Scripts
        };

        /// <summary>
        /// Nombre de la rama en castellano: es el de la carpeta al exportar (no cambia con el idioma de la interfaz)
        /// y el texto que se traduce en el árbol.
        /// </summary>
        public static string Nombre(RamaHmi rama)
        {
            switch (rama)
            {
                case RamaHmi.Pantallas: return "Pantallas";
                case RamaHmi.Plantillas: return "Plantillas";
                case RamaHmi.PopUps: return "Pop-ups";
                case RamaHmi.SlideIns: return "Slide-ins";
                case RamaHmi.ElementosGlobales: return "Elementos globales";
                case RamaHmi.VistaGeneral: return "Vista general de pantallas";
                case RamaHmi.Variables: return "Variables HMI";
                case RamaHmi.Conexiones: return "Conexiones";
                case RamaHmi.Ciclos: return "Ciclos";
                case RamaHmi.ListasTextos: return "Listas de textos";
                case RamaHmi.ListasGraficos: return "Listas de gráficos";
                case RamaHmi.Scripts: return "Scripts VB";
                default: throw new ArgumentOutOfRangeException(nameof(rama));
            }
        }

        /// <summary>Ramas que en TIA admiten carpetas propias.</summary>
        public static bool TieneCarpetas(RamaHmi rama) =>
            rama == RamaHmi.Pantallas || rama == RamaHmi.Plantillas || rama == RamaHmi.PopUps ||
            rama == RamaHmi.Variables || rama == RamaHmi.Scripts;

        /// <summary>Ramas que son un único elemento (no una lista): en el árbol cuelgan directamente de la HMI.</summary>
        public static bool EsUnica(RamaHmi rama) => rama == RamaHmi.ElementosGlobales || rama == RamaHmi.VistaGeneral;

        /// <summary>
        /// Lee solo el principio del XML y devuelve la rama del elemento de HMI que contiene, o null si no es de HMI.
        /// Openness nombra el elemento "Hmi.&lt;espacio&gt;.&lt;tipo&gt;"; se mira solo el tipo.
        /// </summary>
        public static RamaHmi? DetectarRama(string rutaXml)
        {
            try
            {
                var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true };
                using (var lector = XmlReader.Create(rutaXml, ajustes))
                {
                    while (lector.Read())
                    {
                        if (lector.NodeType != XmlNodeType.Element)
                            continue;
                        if (lector.LocalName.StartsWith("SW.", StringComparison.Ordinal))
                            return null;
                        if (!lector.LocalName.StartsWith("Hmi.", StringComparison.Ordinal))
                            continue;

                        switch (lector.LocalName.Substring(lector.LocalName.LastIndexOf('.') + 1))
                        {
                            case "Screen": return RamaHmi.Pantallas;
                            case "ScreenTemplate": return RamaHmi.Plantillas;
                            case "ScreenPopup": return RamaHmi.PopUps;
                            case "ScreenSlidein": return RamaHmi.SlideIns;
                            case "ScreenGlobalElements": return RamaHmi.ElementosGlobales;
                            case "ScreenOverview": return RamaHmi.VistaGeneral;
                            case "TagTable": return RamaHmi.Variables;
                            case "Connection": return RamaHmi.Conexiones;
                            case "Cycle": return RamaHmi.Ciclos;
                            case "TextList": return RamaHmi.ListasTextos;
                            case "GraphicList": return RamaHmi.ListasGraficos;
                            case "VBScript": return RamaHmi.Scripts;
                            default: return null;
                        }
                    }
                }
            }
            catch (XmlException)
            {
            }
            return null;
        }

        /// <summary>
        /// Prepara los XML de una carpeta (y sus subcarpetas) para importarlos en una HMI.
        /// Si la ruta pasa por la carpeta de su rama (como la deja la exportación), cuenta lo que hay después;
        /// si no, toda la ruta. Los archivos que no son de HMI se devuelven aparte.
        /// </summary>
        public static List<ArchivoHmi> PrepararCarpeta(string carpeta, out List<string> descartados)
        {
            var rutas = Directory.GetFiles(carpeta, "*.xml", SearchOption.AllDirectories);
            return Separar(rutas, (ruta, rama) => GruposDentroDeRama(rama, ArchivosBloque.GruposRelativos(carpeta, ruta)), out descartados);
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
        public static IReadOnlyList<string> CarpetasDestino(RamaHmi? ramaSeleccionada, IReadOnlyList<string> gruposSeleccionados, ArchivoHmi archivo)
        {
            if (!TieneCarpetas(archivo.Rama))
                return new string[0];
            return ramaSeleccionada == archivo.Rama ? gruposSeleccionados.Concat(archivo.Grupos).ToList() : archivo.Grupos;
        }

        /// <summary>Lo que hay después de la carpeta con el nombre de la rama; sin ella, toda la ruta.</summary>
        public static IReadOnlyList<string> GruposDentroDeRama(RamaHmi rama, IReadOnlyList<string> grupos)
        {
            if (!TieneCarpetas(rama))
                return new string[0];
            int i = grupos.ToList().FindIndex(g => g.Equals(Nombre(rama), StringComparison.OrdinalIgnoreCase));
            return i < 0 ? grupos : grupos.Skip(i + 1).ToList();
        }

        private static List<ArchivoHmi> Separar(IEnumerable<string> rutas, Func<string, RamaHmi, IReadOnlyList<string>> grupos,
            out List<string> descartados)
        {
            var archivos = new List<ArchivoHmi>();
            descartados = new List<string>();
            foreach (string ruta in rutas)
            {
                var rama = DetectarRama(ruta);
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
