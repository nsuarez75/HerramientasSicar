using System.Collections.Generic;

namespace HerramientasSICAR
{
    /// <summary>Traducciones al inglés de los textos de la interfaz (ver L.cs).</summary>
    public static partial class L
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // ---- Ventana, barra superior y apariencia
            ["Herramientas SICAR"] = "SicarTools",
            ["Utilidades para proyectos SICAR@GST en TIA Portal"] = "Utilities for SICAR@GST projects in TIA Portal",
            ["Proyecto abierto en TIA Portal"] = "Project open in TIA Portal",
            ["Buscar proyectos abiertos en TIA"] = "Find projects open in TIA",
            ["Conectar"] = "Connect",
            ["Idioma"] = "Language",
            ["Tema"] = "Theme",
            ["Sistema"] = "System",
            ["Claro"] = "Light",
            ["Oscuro"] = "Dark",
            ["Acerca de"] = "About",
            ["{0}, versión {1}.\nCreado por Ignacio Suarez (Alomatek)."] = "{0}, version {1}.\nMade by Ignacio Suarez (Alomatek).",
            ["Hay una versión nueva: {0} (tienes la {1})."] = "A new version is available: {0} (you have {1}).",
            ["Descargar"] = "Download",
            ["Cerrar"] = "Close",
            ["CON TIA"] = "WITH TIA",
            ["CON ARCHIVOS"] = "WITH FILES",
            ["Conecta con un proyecto de TIA en la barra superior."] = "Connect to a TIA project in the top bar.",

            // ---- Conexión y estado
            ["Abre el proyecto en TIA Portal, elígelo arriba y pulsa Conectar."] = "Open the project in TIA Portal, pick it above and press Connect.",
            ["Buscando proyectos abiertos en TIA Portal…"] = "Looking for projects open in TIA Portal…",
            ["No hay ningún proyecto abierto en TIA Portal V19."] = "There is no project open in TIA Portal V19.",
            ["{0} proyecto(s) abierto(s) en TIA. Elige uno y pulsa Conectar."] = "{0} project(s) open in TIA. Pick one and press Connect.",
            ["Conectando… (si TIA pide permiso para esta aplicación, acéptalo)"] = "Connecting… (if TIA asks for permission for this application, accept it)",
            ["Conectado, pero el proyecto no tiene ningún PLC ni HMI."] = "Connected, but the project has no PLC or HMI.",
            ["Conectado con {0}."] = "Connected to {0}.",
            ["No se encuentra TIA Portal V19 con Openness en este equipo."] = "TIA Portal V19 with Openness was not found on this computer.",
            ["No se encuentra TIA Portal V19 con Openness en este equipo. Las herramientas con archivos sí funcionan."] =
                "TIA Portal V19 with Openness was not found on this computer. The tools that work with files still work.",
            ["Error: {0}"] = "Error: {0}",
            ["El proyecto ya no está abierto en TIA Portal."] = "The project is no longer open in TIA Portal.",
            ["El proyecto ya no está abierto en TIA Portal. Vuelve a conectar."] = "The project is no longer open in TIA Portal. Connect again.",
            ["No se pudo acceder al proyecto abierto en TIA Portal."] = "Could not access the project open in TIA Portal.",

            // ---- Errores de TIA
            ["No se encontró ningún PLC con software en el proyecto."] = "No PLC with software was found in the project.",
            ["El bloque no es compilable."] = "The block cannot be compiled.",
            ["Archivo XML de importación no encontrado."] = "XML file to import not found.",
            ["está protegido con contraseña (know-how)"] = "is password protected (know-how)",
            ["no compila; revisa sus errores en TIA"] = "does not compile; check its errors in TIA",
            ["no se encuentra el PLC '{0}'"] = "PLC '{0}' not found",
            ["No se encontró el bloque '{0}' en el proyecto."] = "Block '{0}' was not found in the project.",
            ["ya no existe en el proyecto"] = "no longer exists in the project",

            // ---- Diálogos
            ["Aceptar"] = "OK",
            ["Cancelar"] = "Cancel",
            ["Abrir"] = "Open",
            ["Libro de Excel (*.xlsx)"] = "Excel workbook (*.xlsx)",
            ["Bloques exportados (*.xml)"] = "Exported blocks (*.xml)",
            ["Elegir…"] = "Choose…",
            ["Guardar como…"] = "Save as…",

            // ---- Importar / Exportar
            ["Importar / Exportar"] = "Import / Export",
            ["Exporta bloques y elementos de HMI del proyecto a XML e importa XML en el proyecto."] = "Exports project blocks and HMI items to XML and imports XML files into the project.",
            ["PROYECTO"] = "PROJECT",
            ["Filtrar por nombre"] = "Filter by name",
            ["Conecta con un proyecto para ver sus PLC y HMI con todo lo que tienen dentro."] = "Connect to a project to see its PLCs and HMIs with everything inside them.",
            ["Ningún elemento marcado"] = "No items checked",
            ["{0} elemento(s) marcado(s)"] = "{0} item(s) checked",
            ["protegido"] = "protected",
            ["Exportar"] = "Export",
            ["Marca en el árbol los bloques y elementos de HMI (marcar una carpeta marca todo lo que tiene dentro). Cada uno se guarda como XML y las carpetas de TIA se crean como subcarpetas."] =
                "Check the blocks and HMI items in the tree (checking a folder checks everything inside it). Each one is saved as XML and the TIA folders become subfolders.",
            ["Exportar marcados…"] = "Export checked…",
            ["Importar"] = "Import",
            ["Selecciona en el árbol el PLC o la carpeta destino. En una HMI, cada archivo va a su rama; si seleccionas una carpeta de una rama, lo de esa rama entra en ella. Lo que ya exista con el mismo nombre se sobrescribe."] =
                "Select the target PLC or folder in the tree. In an HMI, each file goes to its branch; if you select a folder of a branch, items of that branch go into it. Anything that already exists with the same name is overwritten.",
            ["Destino"] = "Target",
            ["Selecciona en el árbol dónde importar"] = "Select in the tree where to import",
            ["Importar archivos…"] = "Import files…",
            ["Uno o varios XML, directos al destino"] = "One or more XML files, straight into the target",
            ["Importar carpeta…"] = "Import folder…",
            ["Todos los XML de una carpeta; sus subcarpetas se crean como carpetas en TIA"] = "Every XML in a folder; its subfolders become folders in TIA",
            ["La aplicación nunca abre, guarda ni cierra el proyecto. Después de importar, guárdalo en TIA. Los bloques sin compilar se compilan antes de exportar; los protegidos con contraseña no se pueden exportar. De las HMI se ven las WinCC clásicas (Basic, Comfort, Advanced); los avisos, las recetas y los usuarios no se pueden exportar."] =
                "The application never opens, saves or closes the project. After importing, save it in TIA. Uncompiled blocks are compiled before export; password-protected blocks cannot be exported. Only classic WinCC HMIs (Basic, Comfort, Advanced) are shown; alarms, recipes and users cannot be exported.",
            ["Exportando…"] = "Exporting…",
            ["Importando…"] = "Importing…",
            ["Exportando {0}/{1}: {2}"] = "Exporting {0}/{1}: {2}",
            ["Importando {0}/{1}: {2}"] = "Importing {0}/{1}: {2}",
            ["{0} elemento(s) exportado(s) a {1}."] = "{0} item(s) exported to {1}.",
            ["{0} bloque(s) importado(s). Guarda el proyecto en TIA para conservarlos."] = "{0} block(s) imported. Save the project in TIA to keep them.",
            ["{0} con error (ver abajo)."] = "{0} with errors (see below).",
            ["No hay bloques que importar."] = "There are no blocks to import.",
            ["{0}: no es un bloque exportado de TIA, no se importa"] = "{0}: not a block exported from TIA, not imported",
            ["Carpeta donde guardar lo exportado"] = "Folder to save the export",
            ["Archivos a importar"] = "Files to import",
            ["Carpeta con lo que se va a importar (las subcarpetas se crean como carpetas)"] = "Folder with what will be imported (subfolders become folders)",
            ["Importar bloques"] = "Import blocks",
            ["Se van a importar {0} bloque(s) en {1}.\n\nLos bloques que ya existan con el mismo nombre se sobrescriben."] =
                "{0} block(s) will be imported into {1}.\n\nExisting blocks with the same name are overwritten.",
            ["{0}: no es un elemento de HMI exportado de TIA, no se importa"] = "{0}: not an HMI item exported from TIA, not imported",
            ["No hay elementos de HMI que importar."] = "There are no HMI items to import.",
            ["Importar en la HMI"] = "Import into the HMI",
            ["Se van a importar {0} elemento(s) en la HMI {1}, cada uno en su rama.\n\nLo que ya exista con el mismo nombre se sobrescribe."] =
                "{0} item(s) will be imported into HMI {1}, each into its branch.\n\nAnything that already exists with the same name is overwritten.",
            ["{0} elemento(s) importado(s). Guarda el proyecto en TIA para conservarlos."] = "{0} item(s) imported. Save the project in TIA to keep them.",
            ["no se encuentra la HMI '{0}'"] = "HMI '{0}' not found",
            ["TIA Portal se ha cerrado o ya no tiene el proyecto abierto: se para aquí y quedan {0} sin hacer."] =
                "TIA Portal has closed or no longer has the project open: stopping here with {0} left to do.",
            ["la ruta es demasiado larga para Windows ({0} caracteres); exporta a una carpeta con una ruta más corta"] =
                "the path is too long for Windows ({0} characters); export to a folder with a shorter path",
            ["esta HMI no admite ese tipo de elemento"] = "this HMI does not support that kind of item",
            ["Pantallas"] = "Screens",
            ["Plantillas"] = "Templates",
            ["Pop-ups"] = "Pop-ups",
            ["Slide-ins"] = "Slide-ins",
            ["Elementos globales"] = "Global elements",
            ["Vista general de pantallas"] = "Screen overview",
            ["Variables HMI"] = "HMI tags",
            ["Conexiones"] = "Connections",
            ["Ciclos"] = "Cycles",
            ["Listas de textos"] = "Text lists",
            ["Listas de gráficos"] = "Graphic lists",
            ["Scripts VB"] = "VB scripts",

            // ---- DiagExpected
            ["Dado un FB de secuencia, genera un Excel con el estado que deben tener actuadores y sensores en cada paso. Es una primera plantilla: revísala antes de usarla."] =
                "From a sequence FB, generates an Excel file with the state actuators and sensors must have in each step. It is a first template: review it before using it.",
            ["FB de secuencia"] = "Sequence FB",
            ["Nombre del bloque en el proyecto conectado."] = "Name of the block in the connected project.",
            ["Ej.: FB1040_ST10_FB"] = "E.g. FB1040_ST10_FB",
            ["Exportar y generar Excel"] = "Export and generate Excel",
            ["El bloque se compila y se exporta a la carpeta del proyecto (junto al .ap19), y el Excel se guarda allí como «bloque»_DIAG.xlsx."] =
                "The block is compiled and exported to the project folder (next to the .ap19), and the Excel file is saved there as «block»_DIAG.xlsx.",
            ["Exportando {0}…"] = "Exporting {0}…",
            ["Excel generado: {0}"] = "Excel file generated: {0}",
            ["El archivo XML exportado no existe."] = "The exported XML file does not exist.",
            ["No se pudo crear el archivo Excel."] = "Could not create the Excel file.",

            // ---- Numerador
            ["Numerador"] = "Message numbering",
            ["Renumera de 1 en adelante los mensajes de todos los FB de mensajes del proyecto (GeneralMessages_FB y OM1…OM8_Messages_FB). Es el paso previo a generar el DiagAddon."] =
                "Renumbers from 1 onwards the messages of every message FB in the project (GeneralMessages_FB and OM1…OM8_Messages_FB). It is the step before generating the DiagAddon.",
            ["FB de mensajes"] = "Message FBs",
            ["Renumerar mensajes"] = "Renumber messages",
            ["Se compila el proyecto y cada FB de mensajes se exporta, se renumera y se vuelve a importar en su misma carpeta. Los que el proyecto no tiene se saltan. Al terminar, guarda el proyecto en TIA."] =
                "The project is compiled and each message FB is exported, renumbered and imported back into the same folder. Those the project does not have are skipped. When it finishes, save the project in TIA.",
            ["Compilando el proyecto…"] = "Compiling the project…",
            ["Procesando {0}…"] = "Processing {0}…",
            ["El proyecto no tiene ningún FB de mensajes de SICAR."] = "The project has no SICAR message FB.",
            ["{0} FB de mensajes renumerado(s). Guarda el proyecto en TIA para conservarlos."] = "{0} message FB(s) renumbered. Save the project in TIA to keep them.",
            ["{0} FB de mensajes renumerado(s), {1} con error (ver abajo)."] = "{0} message FB(s) renumbered, {1} with errors (see below).",

            // ---- Renumerar Array
            ["Renumerar Array"] = "Renumber array",
            ["Renumera de menor a mayor todos los usos de un array dentro de un FB o FC. Útil, por ejemplo, para intercalar un segmento nuevo en un array de flancos sin reordenarlo a mano. No es exclusivo de SICAR."] =
                "Renumbers in ascending order every use of an array inside an FB or FC. Useful, for example, to insert a new network into an edge array without reordering it by hand. Not specific to SICAR.",
            ["Bloque (FB o FC)"] = "Block (FB or FC)",
            ["Ej.: User_Transfer_Location_FB"] = "E.g. User_Transfer_Location_FB",
            ["Array"] = "Array",
            ["Ej.: AuxTrans"] = "E.g. AuxTrans",
            ["Renumerar"] = "Renumber",
            ["Si el array es de un DB global, basta con el nombre de la variable, sin el del DB. El bloque se exporta, se renumera y se importa encima del original. Al terminar, guarda el proyecto en TIA."] =
                "If the array belongs to a global DB, the variable name is enough, without the DB name. The block is exported, renumbered and imported over the original. When it finishes, save the project in TIA.",
            ["{0}: {1} uso(s) de {2} renumerados. Guarda el proyecto en TIA para conservarlos."] = "{0}: {1} use(s) of {2} renumbered. Save the project in TIA to keep them.",
            ["No se encontró el array '{0}' en el bloque."] = "Array '{0}' was not found in the block.",

            // ---- Comentador
            ["Comentador"] = "Step comments",
            ["Genera el comentario de los pasos de secuencia en castellano, inglés y alemán a partir del título de cada segmento."] =
                "Generates the comment of each sequence step in Spanish, English and German from the title of its network.",
            ["Lista de textos"] = "Text list",
            ["Lista de textos exportada de TIA"] = "Text list exported from TIA",
            ["Excel exportado de TIA (.xlsx)"] = "Excel file exported from TIA (.xlsx)",
            ["Archivo de salida"] = "Output file",
            ["Ej.: TextList_comentada.xlsx"] = "E.g. TextList_commented.xlsx",
            ["Traducir los títulos (inglés, alemán y castellano). Necesita conexión a internet."] = "Translate the titles (English, German and Spanish). Needs an internet connection.",
            ["Generar comentarios"] = "Generate comments",
            ["1. En TIA, exporta los textos del proyecto marcando solo «Block comment»."] = "1. In TIA, export the project texts with only «Block comment» checked.",
            ["2. Elige ese Excel aquí y genera los comentarios. La salida se guarda en la misma carpeta."] = "2. Choose that Excel file here and generate the comments. The output is saved in the same folder.",
            ["3. Importa en TIA el archivo generado con «Import source language» marcado y sin ningún FB de secuencia abierto."] =
                "3. Import the generated file in TIA with «Import source language» checked and no sequence FB open.",
            ["La traducción usa servicios de terceros (Google Translate): su calidad depende de lo claro que sea el título de cada paso."] =
                "The translation uses third-party services (Google Translate): its quality depends on how clear each step title is.",
            ["Leyendo la lista de textos…"] = "Reading the text list…",
            ["Preparando…"] = "Preparing…",
            ["Traduciendo títulos: {0}/{1}"] = "Translating titles: {0}/{1}",
            ["Copiando títulos: {0}/{1}"] = "Copying titles: {0}/{1}",
            ["Lista de textos guardada: {0}"] = "Text list saved: {0}",
            ["La hoja 'User Texts' no existe."] = "The 'User Texts' sheet does not exist.",

            // ---- Generador HMI
            ["Generador HMI"] = "HMI generator",
            ["A partir del Excel de configuración de pantallas HMI (plantilla de GESTAMP), genera la configuración de menús del PLC y las listas de textos, y el código AWL que configura qué imágenes del HMI se ven."] =
                "From the HMI screen configuration Excel (GESTAMP template), generates the PLC menu configuration, the text lists and the STL code that sets which HMI screens are visible.",
            ["Excel de origen"] = "Source Excel",
            ["Configuración de pantallas HMI (.xlsx)"] = "HMI screen configuration (.xlsx)",
            ["Excel de configuración de pantallas HMI"] = "HMI screen configuration Excel",
            ["Excel generado"] = "Generated Excel",
            ["Dónde guardar el resultado (.xlsx)"] = "Where to save the result (.xlsx)",
            ["Guardar el Excel generado como…"] = "Save the generated Excel as…",
            ["Generar Excel"] = "Generate Excel",
            ["Cargando el Excel de origen…"] = "Loading the source Excel…",
            ["No se encontraron pantallas válidas en ninguna hoja del archivo de origen."] = "No valid screens were found in any sheet of the source file.",
            ["Guardando el archivo…"] = "Saving the file…",
            ["Leyendo {0}…"] = "Reading {0}…",
            ["Generando la configuración del PLC…"] = "Generating the PLC configuration…",
            ["Generando las listas de textos…"] = "Generating the text lists…",
            ["Traduciendo las funciones de usuario…"] = "Translating the user functions…",

            // ---- Sincro Checker
            ["Detecta pares de pasos de un FB de secuencia (LAD) cuyos ilocks (ilockAuto e ilockExtSync, junto con PERM) pueden cumplirse a la vez al pasar de manual a automático, lo que da error de sincronización."] =
                "Finds pairs of steps in a sequence FB (LAD) whose ilocks (ilockAuto and ilockExtSync, together with PERM) can be met at the same time when switching from manual to automatic, which causes a synchronisation error.",
            ["Abrir FB exportado…"] = "Open exported FB…",
            ["o exporta del proyecto conectado:"] = "or export from the connected project:",
            ["Nombre del FB de secuencia"] = "Sequence FB name",
            ["Exportar y comprobar"] = "Export and check",
            ["Un actuador no puede estar en -H y -W a la vez"] = "An actuator cannot be in -H and -W at the same time",
            ["Solo un valor activo por señal decodificada (ProcessCode=N, x == N)"] = "Only one active value per decoded signal (ProcessCode=N, x == N)",
            ["Ninguno"] = "None",
            ["Primer paso manual:"] = "First manual step:",
            ["Desde este paso en adelante todos se consideran manuales y no se comprueban"] = "From this step onwards all steps are considered manual and are not checked",
            ["Formato no válido: usa el número de paso, p.ej. S100"] = "Invalid format: use the step number, e.g. S100",
            ["Detectado automáticamente"] = "Detected automatically",
            ["No se ha detectado ningún paso manual. Si la secuencia tiene pasos manuales, indica el primero; si no, déjalo vacío."] =
                "No manual step was detected. If the sequence has manual steps, enter the first one; otherwise leave it empty.",
            ["COINCIDENCIAS"] = "MATCHES",
            ["Volver al estado de ejemplo que provoca la coincidencia"] = "Go back to the example state that causes the match",
            ["Estado de ejemplo"] = "Example state",
            ["DIFERENCIAS ENTRE LOS DOS PASOS (VÁLIDAS EN CUALQUIER ESTADO; EN NARANJA EN EL LAD)"] = "DIFFERENCES BETWEEN THE TWO STEPS (VALID IN ANY STATE; ORANGE IN THE LAD)",
            ["Ningún paso exige siempre una señal que el otro no exija: la coincidencia depende de combinaciones de ramas."] =
                "Neither step always requires a signal the other does not: the match depends on combinations of branches.",
            ["POR QUÉ COINCIDEN EN ESTE ESTADO"] = "WHY THEY MATCH IN THIS STATE",
            ["Señales implicadas (también se cambian con clic en el contacto)"] = "Signals involved (they can also be changed by clicking the contact)",
            ["Señal que diferencia los dos pasos"] = "Signal that differs between the two steps",
            ["Clic para cambiarla"] = "Click to change it",
            ["FB de secuencia exportado"] = "Exported sequence FB",
            ["El archivo elegido es {0}, no un FB de secuencia."] = "The chosen file is {0}, not a sequence FB.",
            ["En la misma carpeta está el FB de secuencia:\n{0}\n\n¿Quieres abrirlo?"] = "The sequence FB is in the same folder:\n{0}\n\nDo you want to open it?",
            ["Esta estación tiene {0} secuencias:\n{1}\n\nElige cuál abrir."] = "This station has {0} sequences:\n{1}\n\nChoose which one to open.",
            ["Secuencia de {0}"] = "Sequence of {0}",
            ["Secuencias de {0}"] = "Sequences of {0}",
            ["Analizando el bloque…"] = "Analysing the block…",
            ["El bloque no tiene pasos automáticos (S###)"] = "The block has no automatic steps (S###)",
            ["{0} pasos automáticos: ningún par de pasos puede sincronizar a la vez"] = "{0} automatic steps: no pair of steps can synchronise at the same time",
            ["{0} pasos automáticos: {1} pares de pasos pueden sincronizar a la vez"] = "{0} automatic steps: {1} pairs of steps can synchronise at the same time",
            ["Se ejecuta en cada ciclo junto al paso activo"] = "Runs every cycle together with the active step",
            ["{0} siempre exige ({1} también las usa, pero admite el valor contrario):"] = "{0} always requires ({1} also uses them, but accepts the opposite value):",
            ["{0} siempre exige ({1} no las comprueba):"] = "{0} always requires ({1} does not check them):",
            ["ilockAuto (no escrito → 1)"] = "ilockAuto (not written → 1)",
            ["Con este estado sincronizan {0} y {1} a la vez → error de sincronización"] = "With this state {0} and {1} synchronise at the same time → synchronisation error",
            ["Con este estado solo sincroniza {0}"] = "With this state only {0} synchronises",
            ["Con este estado no sincroniza ninguno de los dos pasos"] = "With this state neither step synchronises",
            ["Ninguna señal por sí sola evita la coincidencia: hay que cambiar varias a la vez."] = "No single signal avoids the match: several must change at once.",
            ["Ninguna señal por sí sola provoca la coincidencia."] = "No single signal causes the match.",
            ["Las exigen los dos pasos: {0}"] = "Required by both steps: {0}",
            ["Solo las exige {0}: {1}"] = "Required only by {0}: {1}",
            ["Cambiando una sola de estas señales coincidirían: {0}"] = "Changing just one of these signals they would match: {0}",
            ["El archivo no es un bloque de programa sino {0}.\n\nAbre el FB de secuencia exportado (p.ej. FB####_<estación>_FB.xml)."] =
                "The file is not a program block but {0}.\n\nOpen the exported sequence FB (e.g. FB####_<station>_FB.xml).",
            ["una tabla de variables"] = "a tag table",
            ["un DB"] = "a DB",
            ["un tipo de datos (UDT)"] = "a data type (UDT)",
            ["un FB"] = "an FB",
            ["una FC"] = "an FC",
            ["un OB"] = "an OB",
            ["un objeto de tipo {0}"] = "an object of type {0}",
        };
    }
}
