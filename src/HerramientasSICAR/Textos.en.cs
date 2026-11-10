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
            ["Leyendo de nuevo lo importado en {0}…"] = "Reading again what was imported into {0}…",
            ["Leyendo el árbol del proyecto…"] = "Reading the project tree…",
            ["Leyendo el árbol del proyecto: {0} ({1}/{2})…"] = "Reading the project tree: {0} ({1}/{2})…",
            ["Leyendo el árbol del proyecto: {0} › {1} ({2}/{3})…"] = "Reading the project tree: {0} › {1} ({2}/{3})…",
            ["sin leer"] = "not read",
            ["Selecciona un PLC o una HMI para leerlo"] = "Select a PLC or HMI to read it",
            ["Leer {0}"] = "Read {0}",
            ["Leer de nuevo {0}"] = "Read {0} again",
            ["Árbol de {0} leído."] = "{0} tree read.",
            ["Trae de TIA el árbol del PLC o la HMI seleccionado: si no se leyó al conectar, o para ver cambios hechos en TIA"] =
                "Reads the tree of the selected PLC or HMI from TIA: if it was not read when connecting, or to see changes made in TIA",
            ["Conectado con {0}. {1} PLC o HMI sin leer: selecciónalo en el árbol y pulsa Leer."] =
                "Connected to {0}. {1} PLC or HMI not read: select it in the tree and press Read.",
            ["¿Qué quieres leer del proyecto?"] = "What do you want to read from the project?",
            ["Leer el árbol de cada PLC o HMI lleva un rato en proyectos grandes. Los que no marques salen en el árbol sin leer; luego puedes leerlos seleccionándolos y pulsando Leer."] =
                "Reading the tree of each PLC or HMI takes a while in large projects. The ones you leave unchecked appear in the tree as not read; you can read them later by selecting them and pressing Read.",
            ["Leer"] = "Read",
            ["No leer ninguno"] = "Read none",
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
            ["es la tabla de variables estándar de la HMI; no se sobrescribe"] = "it is the HMI's default tag table; it is not overwritten",
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
            ["Configura los menús de las HMI del PLC (nombres de pantallas, qué pantallas ve cada HMI y funciones de usuario) y genera el FC CallHMIMenusConfig_FC y las listas de textos en XML para importarlos en TIA."] =
                "Configures the menus of the PLC's HMIs (screen names, which screens each HMI shows and user functions) and generates the CallHMIMenusConfig_FC FC and the text lists as XML to import them into TIA.",
            ["Texto (para todos los idiomas)"] = "Text (for all languages)",
            ["Inglés (si es distinto)"] = "English (if different)",
            ["Alemán (si es distinto)"] = "German (if different)",
            ["Proyecto sin guardar"] = "Unsaved project",
            ["Solo nombres para la lista {0}: el FC no oculta estas pantallas."] = "Names for the {0} list only: the FC does not hide these screens.",
            ["Sin lista de textos: solo se elige qué posiciones se ven en cada HMI."] = "No text list: you only choose which positions each HMI shows.",
            [" (valores {0} a {1})"] = " (values {0} to {1})",
            ["Nombres en la lista {0}. Si una pantalla no tiene nombre, sale el texto estándar."] = "Names in the {0} list. A screen without a name gets the standard text.",
            ["Proyecto del Generador HMI (*.hmimenus)"] = "HMI generator project (*.hmimenus)",
            ["Cambios sin guardar"] = "Unsaved changes",
            ["El proyecto tiene cambios sin guardar. ¿Descartarlos?"] = "The project has unsaved changes. Discard them?",
            ["Descartar"] = "Discard",
            ["Proyecto nuevo con una HMI."] = "New project with one HMI.",
            ["Abrir un proyecto del Generador HMI"] = "Open an HMI generator project",
            ["Proyecto abierto: {0}"] = "Project opened: {0}",
            ["Guardar el proyecto como…"] = "Save the project as…",
            ["Proyecto guardado: {0}"] = "Project saved: {0}",
            ["Ya hay 8 HMI, el máximo (hay 8 IDB HMIMenusConfig)."] = "There are already 8 HMIs, the maximum (there are 8 HMIMenusConfig IDBs).",
            ["Quitar HMI"] = "Remove HMI",
            ["¿Quitar la HMI {0} del proyecto? Se pierde qué pantallas se ven en ella."] = "Remove HMI {0} from the project? Which screens it shows will be lost.",
            ["Quitar"] = "Remove",
            ["Todos los textos tienen ya su inglés y su alemán."] = "All texts already have their English and German.",
            ["Traducir"] = "Translate",
            ["Se rellenan el inglés y el alemán de {0} textos que no los tienen, con un traductor en línea (necesita internet). Revísalos después eligiendo el idioma arriba."] =
                "English and German are filled in for {0} texts that lack them, with an online translator (needs internet). Review them afterwards by choosing the language above.",
            ["Traduciendo…"] = "Translating…",
            ["Traducidos {0} textos. Revísalos eligiendo Inglés o Alemán arriba."] = "{0} texts translated. Review them by choosing English or German above.",
            ["No se puede generar"] = "Cannot generate",
            ["Carpeta donde generar el FC y las listas de textos"] = "Folder where the FC and the text lists are generated",
            ["Generando…"] = "Generating…",
            ["Generados el FC y {0} listas de textos en {1}"] = "Generated the FC and {0} text lists in {1}",
            ["No hay ninguna HMI."] = "There is no HMI.",
            ["Hay varias HMI con el número {0}."] = "Several HMIs have number {0}.",
            ["El número de la HMI {0} tiene que ir del 1 al 8."] = "The number of HMI {0} must be between 1 and 8.",
            ["La HMI {0} no tiene nombre."] = "HMI {0} has no name.",
            ["El archivo no es un proyecto del Generador HMI."] = "The file is not an HMI generator project.",
            ["Proyecto nuevo"] = "New project",
            ["Abrir un proyecto…"] = "Open a project…",
            ["Guardar"] = "Save",
            ["Guardar como… (exportar la configuración)"] = "Save as… (export the configuration)",
            ["Editar"] = "Edit",
            ["Rellena el inglés y el alemán que falten con un traductor en línea"] = "Fills in missing English and German with an online translator",
            ["Generar FC y listas"] = "Generate FC and lists",
            ["HMI"] = "HMI",
            ["Nombre de la HMI"] = "HMI name",
            ["Número de IDB"] = "IDB number",
            ["Quitar esta HMI"] = "Remove this HMI",
            ["Añadir HMI"] = "Add HMI",
            ["El número de cada HMI elige su IDB (HMIMenusConfigNN_IDB). El FC lleva siempre todas las HMI."] = "Each HMI's number selects its IDB (HMIMenusConfigNN_IDB). The FC always includes all HMIs.",
            ["Pantallas"] = "Screens",
            ["Título del grupo OEM"] = "OEM group title",
            ["Pos."] = "Pos.",
            ["Nombre de la pantalla"] = "Screen name",
            ["Marcar o desmarcar todas en esta HMI"] = "Check or uncheck all in this HMI",
            ["Se ve en esta HMI"] = "Shown on this HMI",
            ["Funciones de usuario"] = "User functions",
            ["El título de cada pantalla es el nombre de su posición en Manual operations › User functions. Una función sin ningún texto sale con los textos estándar; si tiene alguno, lo vacío sale vacío."] =
                "Each screen's title is the name of its position in Manual operations › User functions. A function with no text at all gets the standard texts; if it has any, empty fields stay empty.",
            ["Nombre"] = "Name",
            ["Acción izquierda"] = "Left action",
            ["Acción derecha"] = "Right action",
            ["Feedback izquierda"] = "Left feedback",
            ["Feedback derecha"] = "Right feedback",

            // ---- Sincro Checker
            ["Busca en el programa de un PLC los pares de pasos de cada secuencia que pueden ser candidatos a la vez al pasar de manual a automático, lo que da error de sincronización. Usa las mismas reglas que GST-Diag."] =
                "Finds, in a PLC program, the pairs of steps of each sequence that can be candidates at the same time when switching from manual to automatic, which causes a synchronisation error. It uses the same rules as GST-Diag.",
            ["Exportar el PLC desde TIA y comprobar"] = "Export the PLC from TIA and check",
            ["Abrir una carpeta exportada…"] = "Open an exported folder…",
            ["Se lee el programa entero del PLC, como en GST-Diag: así se siguen las condiciones de activación de las señales y salen los mismos pares en las dos apps."] =
                "The whole PLC program is read, as in GST-Diag: this way the activation conditions of the signals are followed and both apps show the same pairs.",
            ["Carpeta con el programa exportado del PLC"] = "Folder with the exported PLC program",
            ["Buscando los PLC del proyecto…"] = "Looking for the PLCs of the project…",
            ["Exportar desde TIA"] = "Export from TIA",
            ["¿De qué PLC? Se usa el primero que marques."] = "Which PLC? The first one you tick is used.",
            ["Exportar"] = "Export",
            ["Cancelar"] = "Cancel",
            ["Exportando el programa de {0}…"] = "Exporting the program of {0}…",
            ["{0}: protegido con contraseña"] = "{0}: password protected",
            ["{0}: sin compilar"] = "{0}: not compiled",
            ["{0}. {1} objeto(s) no se han exportado (ver abajo): sus señales cuentan como libres."] =
                "{0}. {1} object(s) were not exported (see below): their signals count as free.",
            ["Leyendo el programa exportado…"] = "Reading the exported program…",
            ["No se pudo leer la exportación: {0}"] = "Could not read the export: {0}",
            ["{0} secuencias: ningún par de pasos puede sincronizar a la vez"] = "{0} sequences: no pair of steps can synchronise at the same time",
            ["{0} pares posibles en {1} de {2} secuencias"] = "{0} possible pairs in {1} of {2} sequences",
            ["Señales excluyentes guardadas ({0})"] = "Saved exclusive signals ({0})",
            ["Borrar esta decisión"] = "Delete this decision",
            ["¿Estas señales son excluyentes?"] = "Are these signals exclusive?",
            ["Cada una la exige uno de los dos pasos y el otro no. Si nunca pueden estar a 1 a la vez, márcalas: este par y los que dependan de ellas dejarán de salir."] =
                "Each one is required by one of the two steps and not by the other. If they can never be 1 at the same time, tick them: this pair and those that depend on them will no longer appear.",
            ["Ningún paso exige una señal a 1 que el otro no exija: los dos pasos no se distinguen por sus señales. Revisa sus interlocks en TIA."] =
                "Neither step requires a signal at 1 that the other does not: the two steps cannot be told apart by their signals. Check their interlocks in TIA.",
            ["Marcar como excluyentes"] = "Mark as exclusive",
            ["Secuencia {0}: {1}"] = "Sequence {0}: {1}",
            ["No se pudo guardar la decisión: {0}"] = "Could not save the decision: {0}",

            // ---- Núcleo de GST-Diag (lectura del programa)
            ["La carpeta no tiene bloques exportados: {0}"] = "The folder has no exported blocks: {0}",
            ["No se pudo leer {0}: {1}"] = "Could not read {0}: {1}",
            ["No se han encontrado las OM en el FC983: las secuencias salen sin zona operacional."] =
                "The operation mode areas were not found in FC983: sequences are shown without one.",
            ["{0}, red {1}: {2}"] = "{0}, network {1}: {2}",
            ["La secuencia {0} no tiene paso PERM."] = "Sequence {0} has no PERM step.",
            ["Ningún paso tiene nombre en el idioma de edición {0}. Comprueba el idioma de edición del PLC."] =
                "No step has a name in the editing language {0}. Check the PLC editing language.",
        };
    }
}
