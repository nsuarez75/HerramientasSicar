# Herramientas SICAR

Conjunto de herramientas para automatizar algunas tareas en los proyectos de líneas de producción de GESTAMP basadas en el estándar SICAR@GST V4. En inglés, la app se llama **SicarTools**.

## Requisitos

- Windows de 64 bits. No hace falta instalar .NET: el ejecutable lo lleva dentro.
- TIA Portal V19 instalado, con Openness. Sin él solo funciona el Generador HMI, para editar y generar archivos.
- El usuario de Windows tiene que estar en el grupo **Siemens TIA Openness**.

## Uso

1. Abre el proyecto en TIA Portal V19.
2. Abre la aplicación: busca sola los proyectos abiertos (botón ↻ de la barra superior para volver a buscar). Elige uno y pulsa **Conectar**. La primera vez TIA pregunta si deja entrar a la aplicación: hay que aceptarlo. Si el proyecto tiene varios PLC o HMI, la aplicación pregunta de cuáles leer el árbol (en proyectos grandes lleva minutos) y recuerda la elección; los demás salen «sin leer».
3. Elige la herramienta en el lateral. Todas trabajan sobre el proyecto conectado; sin conexión muestran un aviso y sus botones están desactivados (el Generador HMI sí edita y genera archivos sin conexión).

**Árbol del proyecto, común a todas las herramientas.** El árbol que se lee al conectar (PLC › carpetas › bloques, y las HMI) es el mismo en todas: Importar / Exportar lo enseña entero y las herramientas que trabajan con bloques (Comentador, DiagExpected, Renumerar Array) enseñan solo los bloques que les valen, con un buscador por nombre. Un PLC «sin leer», o uno con cambios hechos en TIA, se lee con el botón **Leer** desde cualquiera de ellas, y lo leído se ve en todas.

La aplicación nunca abre, guarda ni cierra proyectos en TIA: después de una herramienta que cambia bloques, guarda el proyecto en TIA. Si cierras el proyecto en TIA, vuelve a conectar; si TIA se cierra a mitad de un trabajo, la herramienta se para y dice cuántos elementos quedan sin hacer.

La barra inferior dice qué está pasando; si algo falla en varios bloques, se despliega la lista con el motivo. En la barra superior están también el idioma (castellano/inglés), el tema (sistema, claro u oscuro) y «Acerca de». Se recuerdan en `%AppData%\HerramientasSICAR\ajustes.json`, junto con la última carpeta usada. Al arrancar, la aplicación avisa si hay una versión nueva en GitHub.

## Herramientas

### Importar / Exportar

Saca bloques y elementos de HMI del proyecto a archivos XML y los mete de vuelta (en el mismo proyecto o en otro). A la izquierda está el árbol con cada PLC (sus carpetas y bloques) y cada HMI WinCC clásica (sus ramas: pantallas, plantillas, pop-ups, variables, listas, scripts…); el cuadro de arriba filtra por nombre. Detalle de las HMI en [docs/importar-exportar-hmi.md](docs/importar-exportar-hmi.md).

- **Exportar**: marca las casillas de los bloques (marcar una carpeta marca todo lo que tiene dentro; con el filtro puesto, solo lo que se ve) y pulsa **Exportar marcados…**. Cada bloque se guarda como `<nombre>.xml` y las carpetas de TIA se recrean como subcarpetas. Si los bloques son de varios PLC, cada PLC va en su subcarpeta. Los bloques sin compilar se compilan antes de exportar; los protegidos (know-how) no se pueden exportar.
- **Importar**: selecciona en el árbol el PLC o la carpeta destino y pulsa **Importar archivos…** (uno o varios XML, directos a esa carpeta) o **Importar carpeta…** (todos los XML de una carpeta; sus subcarpetas se crean como carpetas en TIA). Los bloques que ya existen con el mismo nombre se sobrescriben (se pide confirmación). Se importan en orden FC → FB → DB → DB de instancia → OB. Después se vuelve a leer el PLC destino.
- **HMI**: se marcan y exportan igual que los bloques (cada rama va en su subcarpeta; las variables, por tablas enteras). Para importar, selecciona cualquier punto de la HMI destino: cada archivo va a su rama y, si lo seleccionado es una carpeta de esa rama, entra en ella. La tabla de variables estándar no se sobrescribe nunca. Los avisos, recetas y usuarios no se pueden exportar (Openness no lo permite) y las HMI Unified no se muestran. Si una ruta pasa del límite de Windows (260 caracteres), exporta a una carpeta más corta, como `C:\Export`.

### Generador HMI

Configura los menús de las HMI de un PLC y los mete en TIA. Todo se edita en la herramienta y se guarda en un archivo de proyecto `.hmimenus` (Nuevo / Abrir / Guardar / Guardar como), que sirve para pasar la configuración de un equipo a otro. Al arrancar se abre el último proyecto usado. Editar y generar no necesita conexión.

- **HMI**: de 1 a 8. El número de cada una elige su IDB (`HMIMenusConfigNN_IDB`).
- **Pantallas**: árbol plegable de menús (OEM 1–8, Installation, Equipment, Manual operations, Hardware diagnostics, Production data y System). Por cada subgrupo, el nombre de cada pantalla, común a todas las HMI, y una casilla por HMI para elegir si se ve. En los grupos OEM también se pone el título del grupo. Pulsando el nombre de una HMI en la cabecera se marcan o desmarcan todas.
- **Funciones de usuario**: 24 pantallas con 8 funciones cada una, cada función con nombre, acción izquierda y derecha y feedback izquierda y derecha.
- **Production data**: cada posición de Process counters, Shift counters, Cycle times y Limit values es un dataset con su lista `SO_ProdData_…_Dataset_NN` de 20 o 10 filas.
- **Idiomas**: cada texto se escribe una vez y sale igual en es-ES, en-US y de-DE. Eligiendo «Inglés» o «Alemán» arriba se puede poner un texto distinto en ese idioma; **Traducir** los rellena con un traductor en línea para revisarlos (los nombres de equipo, con `++`, no se traducen).
- **Importar en TIA…** (pestaña Importar en TIA, con el proyecto conectado): se marca qué importar (el FC `CallHMIMenusConfig_FC`, las 14 listas de menús y funciones y las 32 de Production data, con «Todas / Ninguna» por grupo), el PLC del FC y las HMI de las listas (varias, porque las listas son iguales para todas). Genera en una carpeta temporal, importa y la borra. Si el PLC ya tiene el FC, se sobrescribe en su carpeta; si no, se crea en `08_HMI_Management › 01_HMI_Configuration`.
- **Generar FC y listas** guarda lo mismo en una carpeta: `CallHMIMenusConfig_FC.xml` (FC 1450, con todas las HMI) y `Listas de textos/*.xml` (46 listas).

Las listas llevan siempre todas las entradas: una pantalla sin nombre lleva el texto estándar de su lista (`<<Robot 03>>`…), y una función de usuario o un dataset sin ningún texto, los suyos (`<<S1 L2 Name>>`…). El FC oculta (`alwaysTrue`) o deja ver (`alwaysFalse`) cada posición según las casillas. La especificación está en [`docs/generador-hmi.md`](docs/generador-hmi.md).

### Comentador

Escribe en cada segmento de los FB de secuencia un comentario de tres líneas con el título del paso en inglés, alemán y castellano, directamente en el proyecto conectado:

```
Title_english …
Title_deutsch …
Title_espanol …
```

- A la izquierda, el árbol con los bloques `_FB` de la carpeta `10_Sequence & Messageblocks` de cada PLC, plegables por OM, con casillas. Salen marcados los de secuencia de cada estación (bajo `OM1` a `OM8`), sin Summary, Messages ni `50_Safety`; se pueden marcar o desmarcar a mano.
- **Los títulos están en**: el idioma en que están escritos los títulos (el proyecto está siempre en inglés, pero el texto puede estar en castellano). Esa línea lleva el título tal cual y las otras dos, traducidas desde ese idioma con un traductor en línea (Google Translate y otros), o el mismo texto si no se marca **Traducir**. Los nombres de equipo (con `++`) no se traducen. El idioma elegido se recuerda.
- **Comentar** exporta los FB marcados a una carpeta temporal, escribe el comentario en en-US de cada segmento con título (sin el `S###`; los separadores como `==== SEQUENCE ====` no), sobrescribiendo el que hubiera, los importa en TIA sobrescribiéndolos, los compila y borra la carpeta. Guarda antes el proyecto y cierra los FB que vayas a comentar.

### Sincro Checker

Al pasar de manual a automático, el motor de secuencia SICAR busca el paso cuyos ilocks se cumplen y salta a él; si se cumplen en varios pasos a la vez, da error de sincronización.
Sincro Checker detecta esa situación al programar. Lee el programa entero de un PLC y busca, en cada secuencia, pares de pasos que puedan ser candidatos a la vez.

- «Exportar el PLC desde TIA y comprobar» exporta del proyecto conectado (si tiene varios PLC, pregunta cuál) los bloques, las tablas de variables y los tipos del PLC a una carpeta temporal que se borra en cuanto se ha leído: en la carpeta del proyecto no queda nada. No compila nada: los bloques protegidos o sin compilar se saltan y se avisa abajo (sus señales cuentan como libres). «Abrir una carpeta exportada…» lee una exportación ya hecha (carpetas `Bloques`, `Variables` y `Tipos`), sin conexión.
- Reglas (manual SICAR 3.4.1): al sincronizar se prueban todos los pasos que no se salta un salto; un paso es ejecutable con `ilockAuto` = 1, `ilockExtSync` = 1 y `transAuto` = 0. Las condiciones de PERM se suman con Y a las del paso (en PERM, lo no programado vale 1). En un paso, `ilockAuto` y `transAuto` sin programar valen 0 (un paso manual nunca es ejecutable) e `ilockExtSync` vale 1. `ilockNoSync` e `ilockExtInt` no participan. Durante la sincronización `#Sequence.pos_Pulse_OM_Sync` vale 1.
- Además: una señal que el programa calcula con una sola bobina vale sus condiciones de activación (hasta 10 niveles); un actuador no está en `-H` y `-W` a la vez; `…-ReleasePick` y `…-ReleaseDrop` de una misma estación (mismo nombre delante, p. ej. `++1010+FIX01-`) no están a 1 a la vez; una señal no tiene dos valores (`…=200` y `…=210`, `x == 200` y `x == 210`); un par en el que un paso tiene un salto al otro (un paso de espera y el paso de decisión al que salta) no es doble sincronización; en las secuencias de robot (las que tienen pasos S223–S227) esos pasos no se comprueban; `alwaysTrue` y `alwaysFalse` son constantes.
- Para cada par se proponen las señales que un paso exige a 1 y el otro no (si son menos de dos, no hay nada que proponer y se avisa de revisar sus interlocks). Si quien conoce la máquina sabe que nunca están a 1 a la vez, las marca como excluyentes y la app recalcula. Las decisiones se guardan y se pueden borrar: las de un PLC exportado desde TIA, en `%AppData%\HerramientasSICAR\Sincro\<proyecto>\<PLC>.json` (si había un `sincro.json` de versiones anteriores en la carpeta del proyecto, se aprovechan sus excluyentes); las de una carpeta abierta a mano, en `sincro.json` dentro de esa carpeta.
- El panel de la decisión sobre el par y el de las decisiones guardadas se pliegan, y su alto se cambia arrastrando la separación con la lista de pares.
- Límites: las señales que vienen de fuera del PLC (entradas, robot, otros PLC) son libres, y las redes en STL y SCL todavía no se leen. Por eso en los robots salen muchos pares.
- El código de la comprobación está en `Core/` (ver Estructura).

### DiagExpected

Dado un FB de secuencia, genera un Excel con el estado necesario en cada paso de actuadores y sensores. El FB se elige en el árbol, que solo enseña los FB de secuencia de las estaciones, plegables por OM. Al pulsar **Exportar y generar Excel**, el bloque se compila si hace falta y se exporta a una carpeta temporal que se borra al terminar, y el Excel se guarda en la carpeta del proyecto (junto al `.ap19`) como `<bloque>_DIAG.xlsx`.
Es necesario revisar el contenido generado, ya que no funciona de manera perfecta: sirve para sacar una primera plantilla sobre la que trabajar.

### Numerador

Renumera de 1 a X los mensajes de los FB de mensajes (`GeneralMessages_FB` y `OM1_Messages_FB` … `OM8_Messages_FB`) de **todos los PLC** del proyecto. Es el paso previo a generar el DiagAddon. Se compila el proyecto y cada FB de mensajes se exporta, se renumera y se vuelve a importar en su misma carpeta; los que el proyecto no tiene se saltan.

### Renumerar Array

No es específica del estándar SICAR@GST. Renumera 1, 2, 3… los usos con índice fijo de un array dentro de un FB o FC, en el orden en que aparecen.
Útil cuando tienes un array de flancos, por ejemplo, y necesitas intercalar un segmento nuevo sin calcular el último usado ni reordenarlos todos.

- El bloque se elige en el árbol (solo FB y FC, sin los protegidos).
- Al elegirlo se exporta a una carpeta temporal y se listan los arrays que su código usa con índice fijo, con su número de usos y sus índices actuales (así se ven los huecos o repetidos).
- Se elige el array y se pulsa **Renumerar**: el bloque se exporta, se renumeran los usos con índice fijo de ese array, se importa encima del original y se vuelve a leer. Los usos con índice variable (`miArray[#i]`) no se tocan.

## Compilar

.NET 10 (SDK 10.0) y Avalonia 12. Abrir `HerramientasSICAR.sln` en Visual Studio o `dotnet build HerramientasSICAR.sln -c Release`.

Para sacar el ejecutable de una release, un único `HerramientasSICAR.exe` de 64 bits con .NET y las librerías dentro:

```
dotnet publish src/HerramientasSICAR/HerramientasSICAR.csproj -c Release -r win-x64
```

Con TIA se habla a través de [TIA-Connector](https://github.com/nsuarez75/TIA-Connector), que está como submódulo de git en `external/TIA-Connector`. Al clonar: `git clone --recurse-submodules` (o, si ya está clonado, `git submodule update --init`). Su cliente lleva dentro el puente, un `.exe` aparte que es el único que carga Openness (`Siemens.Engineering.dll`, de la instalación de TIA Portal V19); la app lo extrae a `%LOCALAPPDATA%\TIA-Connector\bridge\` y lo arranca la primera vez que necesita TIA. Para traer una versión nueva de TIA-Connector: actualizar el submódulo, commit y compilar.

## Comprobar antes de un commit

`./checks.sh` compila en Release con los avisos como errores y pasa los tests (`HerramientasSICAR.Tests`, solo la lógica que no necesita TIA, y que todos los textos tengan traducción al inglés). Termina en 0 solo si todo está bien. Necesita TIA Portal V19 instalado para compilar.

## Estructura

El código de la app está en `src/HerramientasSICAR/` y los tests en `tests/HerramientasSICAR.Tests/`. El código (clases, métodos y variables) está en inglés; los comentarios y los textos de la interfaz, en castellano. Los tests se llaman `Método_Escenario_Resultado`. Dentro de `src/HerramientasSICAR/`:

- **Todo lo que habla con TIA pasa por TIA-Connector** (`TiaClient` y sus modelos: `BlockRef`, `PlcTree`, `HmiBranch`, `BatchResult`…); ninguna herramienta llama a Openness. `ViewModels/ToolContext.cs` arranca el cliente (`TiaAsync`), conecta y guarda el proyecto conectado y el árbol leído, y avisa a todas las herramientas cuando cambia. `Helpers/TiaTexts.cs` compone en el idioma de la app el progreso, los fallos y los errores que TIA-Connector devuelve como códigos. En las herramientas, `Tool` tiene lo común (elegir PLC, progreso en la barra de estado) y `BlockSelector` es el árbol de bloques filtrado que usan Comentador, DiagExpected y Renumerar Array. Lo que necesita una carpeta temporal (Comentador, DiagExpected, Sincro Checker, Generador HMI) usa `TempFolder` de TIA-Connector.
- `Helpers/`: la lógica de cada herramienta (Excel de DiagExpected, traductor en línea…) y `TiaTexts.cs`. `Logic/`: qué XML se importan y en qué orden (bloques y HMI; las reglas de nombres, rutas y ramas de HMI son las de TIA-Connector: `FileRules` y `HmiRules`), `sincro.json` de Sincro Checker, comentarios de los FB de secuencia del Comentador (`SequenceComments.cs`), arrays con índice fijo de Renumerar Array (`BlockArrays.cs`), comprobación de versión y, en `Logic/Menus/`, el catálogo de menús, el proyecto `.hmimenus` y la generación del FC y las listas del Generador HMI.
- `Core/`: lectura del programa exportado y comprobación de la doble sincronización (Lad, Logic, SimaticMl, Sicar, Symbols, Programs y `Diagnostics/ValueSource.cs`), en el espacio de nombres `HerramientasSICAR.Core`. Usa `Regex` normal en lugar de `GeneratedRegex`. Sus tests están en `tests/HerramientasSICAR.Tests/Core/`.
- `ViewModels/`, `Views/`: la ventana, con Avalonia 12 y CommunityToolkit.Mvvm. Cada herramienta es un `Tool` con su vista; `ToolContext` es lo que comparten (conexión, árbol del proyecto, diálogos y barra de estado). `Views/Theme.axaml` tiene los colores y estilos de la app; `Controls/Icon.cs`, los iconos (Lucide).
- `L.cs` y `Texts.en.cs`: los textos se escriben en castellano y se traducen al inglés con `L.T`. Al añadir un texto, añadir su traducción (el test lo comprueba).

## Licencia

El código se publica con licencia [MIT](LICENSE) © 2026 Ignacio Suarez. Las librerías de terceros que van dentro del
ejecutable tienen sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

La licencia no cubre el logotipo de Alomatek ni las marcas de terceros (Siemens, TIA Portal, Openness…), que pertenecen a sus dueños.
