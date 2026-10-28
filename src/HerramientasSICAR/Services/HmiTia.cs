using HerramientasSICAR.Logica;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.RuntimeScripting;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HerramientasSICAR.Services
{
    /// <summary>
    /// Lectura, exportación e importación de las ramas de una HMI WinCC clásica. Solo se usa en el hilo de Openness.
    /// </summary>
    internal static class HmiTia
    {
        // Nombre de archivo fijo de las ramas que son un único elemento.
        private const string ArchivoElementosGlobales = "ElementosGlobales";
        private const string ArchivoVistaGeneral = "VistaGeneral";

        /// <summary>Todas las ramas de la HMI. <paramref name="alLeerRama"/> se llama antes de leer cada una.</summary>
        public static HmiInfo Leer(HmiTarget hmi, Action<RamaHmi> alLeerRama = null)
        {
            var info = new HmiInfo { Nombre = hmi.Name };
            info.Ramas.AddRange(LeerRamas(hmi, ArchivosHmi.OrdenArbol, alLeerRama));
            return info;
        }

        /// <summary>Las ramas pedidas, en el orden del árbol.</summary>
        public static List<RamaInfo> LeerRamas(HmiTarget hmi, IEnumerable<RamaHmi> ramas, Action<RamaHmi> alLeerRama = null)
        {
            var pedidas = new HashSet<RamaHmi>(ramas);
            var leidas = new List<RamaInfo>();
            foreach (var rama in ArchivosHmi.OrdenArbol.Where(pedidas.Contains))
            {
                alLeerRama?.Invoke(rama);
                // Algunos paneles no tienen todas las ramas (p. ej. sin vista general): esas no se muestran.
                var raiz = LeerRama(hmi, rama);
                if (raiz != null)
                    leidas.Add(new RamaInfo { Rama = rama, Raiz = raiz });
            }
            return leidas;
        }

        private static CarpetaHmiInfo LeerRama(HmiTarget hmi, RamaHmi rama)
        {
            switch (rama)
            {
                case RamaHmi.Pantallas:
                    return Leer<ScreenFolder>(hmi.ScreenFolder, c => c.Name, c => c.Folders, c => c.Screens.Select(e => e.Name));
                case RamaHmi.Plantillas:
                    return Leer<ScreenTemplateFolder>(hmi.ScreenTemplateFolder, c => c.Name, c => c.Folders, c => c.ScreenTemplates.Select(e => e.Name));
                case RamaHmi.PopUps:
                    return Leer<ScreenPopupFolder>(hmi.ScreenPopupFolder, c => c.Name, c => c.Folders, c => c.ScreenPopups.Select(e => e.Name));
                case RamaHmi.Variables:
                    return Leer<TagFolder>(hmi.TagFolder, c => c.Name, c => c.Folders, c => c.TagTables.Select(e => e.Name));
                case RamaHmi.Scripts:
                    return Leer<VBScriptFolder>(hmi.VBScriptFolder, c => c.Name, c => c.Folders, c => c.VBScripts.Select(e => e.Name));
                case RamaHmi.SlideIns:
                    return hmi.ScreenSlideinFolder == null ? null
                        : Lista(hmi.ScreenSlideinFolder.ScreenSlideins.Select(e => e.SlideinType.ToString()));
                case RamaHmi.Conexiones:
                    return Lista(hmi.Connections.Select(e => e.Name));
                case RamaHmi.Ciclos:
                    return Lista(hmi.Cycles.Select(e => e.Name));
                case RamaHmi.ListasTextos:
                    return Lista(hmi.TextLists.Select(e => e.Name));
                case RamaHmi.ListasGraficos:
                    return Lista(hmi.GraphicLists.Select(e => e.Name));
                case RamaHmi.ElementosGlobales:
                    return hmi.ScreenGlobalElements == null ? null : new CarpetaHmiInfo();
                case RamaHmi.VistaGeneral:
                    return hmi.ScreenOverview == null ? null : new CarpetaHmiInfo();
                default:
                    throw new ArgumentOutOfRangeException(nameof(rama));
            }
        }

        private static CarpetaHmiInfo Leer<T>(T carpeta, Func<T, string> nombre, Func<T, System.Collections.IEnumerable> subcarpetas,
            Func<T, IEnumerable<string>> elementos)
            where T : class
        {
            if (carpeta == null)
                return null;
            var info = new CarpetaHmiInfo { Nombre = nombre(carpeta) };
            foreach (T subcarpeta in subcarpetas(carpeta))
                info.Carpetas.Add(Leer(subcarpeta, nombre, subcarpetas, elementos));
            info.Elementos.AddRange(elementos(carpeta));
            return info;
        }

        private static CarpetaHmiInfo Lista(IEnumerable<string> elementos)
        {
            var info = new CarpetaHmiInfo();
            info.Elementos.AddRange(elementos);
            return info;
        }

        /// <summary>Exporta el elemento a &lt;carpeta&gt;\&lt;nombre&gt;.xml.</summary>
        public static void Exportar(HmiTarget hmi, ElementoHmiRef referencia, string carpeta)
        {
            Action<FileInfo> exportar = ExportadorDe(hmi, referencia)
                ?? throw new InvalidOperationException(L.T("ya no existe en el proyecto"));

            string nombre = referencia.Rama == RamaHmi.ElementosGlobales ? ArchivoElementosGlobales
                : referencia.Rama == RamaHmi.VistaGeneral ? ArchivoVistaGeneral
                : referencia.Nombre;

            string ruta = Path.Combine(carpeta, ArchivosBloque.NombreSeguro(nombre) + ".xml");
            if (ArchivosBloque.RutaDemasiadoLarga(ruta))
                throw new InvalidOperationException(L.T("la ruta es demasiado larga para Windows ({0} caracteres); exporta a una carpeta con una ruta más corta", ruta.Length));

            Directory.CreateDirectory(carpeta);
            var destino = new FileInfo(ruta);
            if (destino.Exists)
                destino.Delete();
            exportar(destino);
        }

        private static Action<FileInfo> ExportadorDe(HmiTarget hmi, ElementoHmiRef r)
        {
            var opciones = ExportOptions.WithDefaults;
            switch (r.Rama)
            {
                case RamaHmi.Pantallas:
                    var pantalla = CarpetaPantallas(hmi, r.Grupos, false)?.Screens.Find(r.Nombre);
                    return pantalla == null ? null : (Action<FileInfo>)(f => pantalla.Export(f, opciones));
                case RamaHmi.Plantillas:
                    var plantilla = CarpetaPlantillas(hmi, r.Grupos, false)?.ScreenTemplates.Find(r.Nombre);
                    return plantilla == null ? null : (Action<FileInfo>)(f => plantilla.Export(f, opciones));
                case RamaHmi.PopUps:
                    var popup = CarpetaPopUps(hmi, r.Grupos, false)?.ScreenPopups.Find(r.Nombre);
                    return popup == null ? null : (Action<FileInfo>)(f => popup.Export(f, opciones));
                case RamaHmi.Variables:
                    var tabla = CarpetaVariables(hmi, r.Grupos, false)?.TagTables.Find(r.Nombre);
                    return tabla == null ? null : (Action<FileInfo>)(f => tabla.Export(f, opciones));
                case RamaHmi.Scripts:
                    var script = CarpetaScripts(hmi, r.Grupos, false)?.VBScripts.Find(r.Nombre);
                    return script == null ? null : (Action<FileInfo>)(f => script.Export(f, opciones));
                case RamaHmi.SlideIns:
                    var slidein = hmi.ScreenSlideinFolder?.ScreenSlideins.FirstOrDefault(e => e.SlideinType.ToString() == r.Nombre);
                    return slidein == null ? null : (Action<FileInfo>)(f => slidein.Export(f, opciones));
                case RamaHmi.Conexiones:
                    var conexion = hmi.Connections.Find(r.Nombre);
                    return conexion == null ? null : (Action<FileInfo>)(f => conexion.Export(f, opciones));
                case RamaHmi.Ciclos:
                    var ciclo = hmi.Cycles.Find(r.Nombre);
                    return ciclo == null ? null : (Action<FileInfo>)(f => ciclo.Export(f, opciones));
                case RamaHmi.ListasTextos:
                    var listaTextos = hmi.TextLists.Find(r.Nombre);
                    return listaTextos == null ? null : (Action<FileInfo>)(f => listaTextos.Export(f, opciones));
                case RamaHmi.ListasGraficos:
                    var listaGraficos = hmi.GraphicLists.Find(r.Nombre);
                    return listaGraficos == null ? null : (Action<FileInfo>)(f => listaGraficos.Export(f, opciones));
                case RamaHmi.ElementosGlobales:
                    var globales = hmi.ScreenGlobalElements;
                    return globales == null ? null : (Action<FileInfo>)(f => globales.Export(f, opciones));
                case RamaHmi.VistaGeneral:
                    var vista = hmi.ScreenOverview;
                    return vista == null ? null : (Action<FileInfo>)(f => vista.Export(f, opciones));
                default:
                    throw new ArgumentOutOfRangeException(nameof(r));
            }
        }

        /// <summary>Importa el XML en su rama, dentro de las carpetas indicadas (se crean si faltan). Lo que ya existe se sobrescribe.</summary>
        public static void Importar(HmiTarget hmi, ArchivoHmi archivo, IReadOnlyList<string> carpetas)
        {
            var xml = new FileInfo(archivo.Ruta);
            var opciones = ImportOptions.Override;
            switch (archivo.Rama)
            {
                case RamaHmi.Pantallas: CarpetaPantallas(hmi, carpetas, true).Screens.Import(xml, opciones); break;
                case RamaHmi.Plantillas: CarpetaPlantillas(hmi, carpetas, true).ScreenTemplates.Import(xml, opciones); break;
                case RamaHmi.PopUps: CarpetaPopUps(hmi, carpetas, true).ScreenPopups.Import(xml, opciones); break;
                case RamaHmi.Variables:
                    // Sobrescribir la tabla de variables estándar ha llegado a cerrar TIA: nunca se toca.
                    string tabla = ArchivosHmi.LeerNombre(archivo.Ruta);
                    if (tabla != null && tabla.Equals(hmi.TagFolder?.DefaultTagTable?.Name, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(L.T("es la tabla de variables estándar de la HMI; no se sobrescribe"));
                    CarpetaVariables(hmi, carpetas, true).TagTables.Import(xml, opciones);
                    break;
                case RamaHmi.Scripts: CarpetaScripts(hmi, carpetas, true).VBScripts.Import(xml, opciones); break;
                case RamaHmi.SlideIns: SinRama(hmi.ScreenSlideinFolder).ScreenSlideins.Import(xml, opciones); break;
                case RamaHmi.Conexiones: hmi.Connections.Import(xml, opciones); break;
                case RamaHmi.Ciclos: hmi.Cycles.Import(xml, opciones); break;
                case RamaHmi.ListasTextos: hmi.TextLists.Import(xml, opciones); break;
                case RamaHmi.ListasGraficos: hmi.GraphicLists.Import(xml, opciones); break;
                case RamaHmi.ElementosGlobales: hmi.ImportScreenGlobalElements(xml, opciones); break;
                case RamaHmi.VistaGeneral: hmi.ImportScreenOverview(xml, opciones); break;
                default: throw new ArgumentOutOfRangeException(nameof(archivo));
            }
        }

        private static T SinRama<T>(T carpeta) where T : class =>
            carpeta ?? throw new InvalidOperationException(L.T("esta HMI no admite ese tipo de elemento"));

        private static ScreenFolder CarpetaPantallas(HmiTarget hmi, IReadOnlyList<string> grupos, bool crear) =>
            Navegar<ScreenFolder>(SinRama(hmi.ScreenFolder), grupos, crear, (c, n) => c.Folders.Find(n), (c, n) => c.Folders.Create(n));

        private static ScreenTemplateFolder CarpetaPlantillas(HmiTarget hmi, IReadOnlyList<string> grupos, bool crear) =>
            Navegar<ScreenTemplateFolder>(SinRama(hmi.ScreenTemplateFolder), grupos, crear, (c, n) => c.Folders.Find(n), (c, n) => c.Folders.Create(n));

        private static ScreenPopupFolder CarpetaPopUps(HmiTarget hmi, IReadOnlyList<string> grupos, bool crear) =>
            Navegar<ScreenPopupFolder>(SinRama(hmi.ScreenPopupFolder), grupos, crear, (c, n) => c.Folders.Find(n), (c, n) => c.Folders.Create(n));

        private static TagFolder CarpetaVariables(HmiTarget hmi, IReadOnlyList<string> grupos, bool crear) =>
            Navegar<TagFolder>(SinRama(hmi.TagFolder), grupos, crear, (c, n) => c.Folders.Find(n), (c, n) => c.Folders.Create(n));

        private static VBScriptFolder CarpetaScripts(HmiTarget hmi, IReadOnlyList<string> grupos, bool crear) =>
            Navegar<VBScriptFolder>(SinRama(hmi.VBScriptFolder), grupos, crear, (c, n) => c.Folders.Find(n), (c, n) => c.Folders.Create(n));

        /// <summary>Baja por las carpetas; si falta una, la crea o devuelve null según <paramref name="crear"/>.</summary>
        private static T Navegar<T>(T raiz, IReadOnlyList<string> grupos, bool crear, Func<T, string, T> buscar, Func<T, string, T> crearCarpeta)
            where T : class
        {
            var carpeta = raiz;
            foreach (string nombre in grupos)
            {
                var siguiente = buscar(carpeta, nombre);
                if (siguiente == null)
                {
                    if (!crear)
                        return null;
                    siguiente = crearCarpeta(carpeta, nombre);
                }
                carpeta = siguiente;
            }
            return carpeta;
        }
    }
}
