# Herramientas SICAR

Conjunto de herramientas para automatizar algunas tareas en los proyectos de líneas de producción de GESTAMP basadas en el estándar SICAR@GST V4. En inglés, la app se llama **SicarTools**.

## Requisitos

- Windows con .NET Framework 4.8.
- TIA Portal V19 instalado, con Openness. El Generador HMI, que trabaja con archivos, funciona sin él.
- El usuario de Windows tiene que estar en el grupo **Siemens TIA Openness**.

## Uso

1. Abre el proyecto en TIA Portal V19.
2. Abre la aplicación: busca sola los proyectos abiertos (botón ↻ de la barra superior para volver a buscar). Elige uno y pulsa **Conectar**. La primera vez TIA pregunta si deja entrar a la aplicación: hay que aceptarlo. Si el proyecto tiene varios PLC o HMI, la aplicación pregunta de cuáles leer el árbol (en proyectos grandes lleva minutos) y recuerda la elección; los demás salen «sin leer» y se leen después con el botón **Leer** de Importar / Exportar.
3. Elige la herramienta en el lateral. Las del grupo **Con TIA** trabajan sobre el proyecto conectado; sin conexión muestran un aviso y sus botones están desactivados. Las del grupo **Con archivos** no necesitan TIA.

La aplicación nunca abre, guarda ni cierra proyectos en TIA: después de una herramienta que cambia bloques, guarda el proyecto en TIA. Si cierras el proyecto en TIA, vuelve a conectar.

La barra inferior dice qué está pasando; si algo falla en varios bloques, se despliega la lista con el motivo. En la barra superior están también el idioma (castellano/inglés), el tema (sistema, claro u oscuro) y «Acerca de». Se recuerdan en `%AppData%\HerramientasSICAR\ajustes.json`, junto con la última carpeta usada. Al arrancar, la aplicación avisa si hay una versión nueva en GitHub.

## Herramientas

### Con TIA

#### Importar / Exportar

Saca bloques y elementos de HMI del proyecto a archivos XML y los mete de vuelta (en el mismo proyecto o en otro). A la izquierda está el árbol con cada PLC (sus carpetas y bloques) y cada HMI WinCC clásica (sus ramas: pantallas, plantillas, pop-ups, variables, listas, scripts…); el cuadro de arriba filtra por nombre. Detalle de las HMI en [docs/importar-exportar-hmi.md](docs/importar-exportar-hmi.md).

- **Exportar**: marca las casillas de los bloques (marcar una carpeta marca todo lo que tiene dentro; con el filtro puesto, solo lo que se ve) y pulsa **Exportar marcados…**. Cada bloque se guarda como `<nombre>.xml` y las carpetas de TIA se recrean como subcarpetas. Si los bloques son de varios PLC, cada PLC va en su subcarpeta. Los bloques sin compilar se compilan antes de exportar; los protegidos (know-how) no se pueden exportar.
- **Importar**: selecciona en el árbol el PLC o la carpeta destino y pulsa **Importar archivos…** (uno o varios XML, directos a esa carpeta) o **Importar carpeta…** (todos los XML de una carpeta; sus subcarpetas se crean como carpetas en TIA). Los bloques que ya existen con el mismo nombre se sobrescriben (se pide confirmación). Se importan en orden FC → FB → DB → DB de instancia → OB.
- **HMI**: se marcan y exportan igual que los bloques (cada rama va en su subcarpeta; las variables, por tablas enteras). Para importar, selecciona cualquier punto de la HMI destino: cada archivo va a su rama y, si lo seleccionado es una carpeta de esa rama, entra en ella. La tabla de variables estándar no se sobrescribe nunca. Los avisos, recetas y usuarios no se pueden exportar (Openness no lo permite) y las HMI Unified no se muestran. Si una ruta pasa del límite de Windows (260 caracteres), exporta a una carpeta más corta, como `C:\Export`.

#### DiagExpected

Dado un FB de secuencia, genera un Excel con el estado necesario en cada paso de actuadores y sensores. El bloque se compila y se exporta a la carpeta del proyecto, y el Excel se guarda allí como `<bloque>_DIAG.xlsx`.
Es necesario revisar el contenido generado, ya que no funciona de manera perfecta: sirve para sacar una primera plantilla sobre la que trabajar.

#### Numerador

Renumera de 1 a X los mensajes de los FB de mensajes del proyecto (`GeneralMessages_FB` y `OM1_Messages_FB` … `OM8_Messages_FB`). Es el paso previo a generar el DiagAddon. Se compila el proyecto y cada FB de mensajes se exporta, se renumera y se vuelve a importar en su misma carpeta; los que el proyecto no tiene se saltan.

#### Renumerar Array

No es específica del estándar SICAR@GST. Coge una variable Array, del tipo que sea, en un FB/FC dado y renumera todas sus apariciones de menor a mayor.
Útil cuando tienes un array de flancos, por ejemplo, y necesitas intercalar un segmento nuevo sin calcular el último usado ni reordenarlos todos.
Si la variable pertenece a un DB global, no hace falta indicar el nombre del DB, solo el de la variable.

#### Sincro Checker

Al pasar de manual a automático, el motor de secuencia SICAR busca el paso cuyos ilocks se cumplen y salta a él; si se cumplen en varios pasos a la vez, da error de sincronización.
Sincro Checker detecta esa situación al programar, con la misma comprobación que GST-Diag: con el mismo programa salen los mismos pares en las dos apps. Lee el programa entero de un PLC y busca, en cada secuencia, pares de pasos que puedan ser candidatos a la vez.

- «Exportar el PLC desde TIA y comprobar» exporta del proyecto conectado los bloques, las tablas de variables y los tipos del PLC a `<carpeta del proyecto>\HerramientasSICAR\Sincro\<PLC>`. No compila nada: los bloques protegidos o sin compilar se saltan y se avisa abajo (sus señales cuentan como libres). «Abrir una carpeta exportada…» lee una exportación ya hecha, también la de GST-Diag, sin conexión.
- Reglas (manual SICAR 3.4.1): al sincronizar se prueban todos los pasos que no se salta un salto; un paso es ejecutable con `ilockAuto` = 1, `ilockExtSync` = 1 y `transAuto` = 0. Las condiciones de PERM se suman con Y a las del paso (en PERM, lo no programado vale 1). En un paso, `ilockAuto` y `transAuto` sin programar valen 0 (un paso manual nunca es ejecutable) e `ilockExtSync` vale 1. `ilockNoSync` e `ilockExtInt` no participan. Durante la sincronización `#Sequence.pos_Pulse_OM_Sync` vale 1.
- Además: una señal que el programa calcula con una sola bobina vale sus condiciones de activación (hasta 10 niveles); un actuador no está en `-H` y `-W` a la vez; una señal no tiene dos valores (`…=200` y `…=210`, `x == 200` y `x == 210`); en las secuencias de robot (las que tienen pasos S223–S227) esos pasos no se comprueban; `alwaysTrue` y `alwaysFalse` son constantes.
- Para cada par se proponen las señales que un paso exige a 1 y el otro no. Si quien conoce la máquina sabe que nunca están a 1 a la vez, las marca como excluyentes y la app recalcula. Las decisiones se guardan en `sincro.json`, junto al programa exportado, y se pueden borrar.
- Límites: las señales que vienen de fuera del PLC (entradas, robot, otros PLC) son libres, y las redes en STL y SCL todavía no se leen. Por eso en los robots salen muchos pares.
- El código de la comprobación es el de GST-Diag, copiado en `Nucleo/` (ver Estructura).

### Con archivos

#### Comentador

Escribe en cada segmento de los FB de secuencia un comentario de tres líneas con el título del paso en inglés, alemán y castellano, directamente en el proyecto conectado:

```
Title_english …
Title_deutsch …
Title_espanol …
```

- **Buscar FB de secuencia** lista los bloques `_FB` de la carpeta `10_Sequence & Messageblocks` y sus subcarpetas (si hay varios PLC, pregunta cuál). Salen marcados los de secuencia de cada estación (bajo `OM1` a `OM8`), sin Summary, Messages ni `50_Safety`; se pueden marcar o desmarcar a mano.
- **Los títulos están en**: el idioma en que están escritos los títulos (el proyecto está siempre en inglés, pero el texto puede estar en castellano). Esa línea lleva el título tal cual y las otras dos, traducidas desde ese idioma con un traductor en línea (Google Translate y otros), o el mismo texto si no se marca **Traducir**. Los nombres de equipo (con `++`) no se traducen.
- **Comentar** exporta los FB marcados a una carpeta temporal, escribe el comentario en en-US de cada segmento con título (sin el `S###`; los separadores como `==== SEQUENCE ====` no), sobrescribiendo el que hubiera, los importa en TIA sobrescribiéndolos, los compila y borra la carpeta. Guarda antes el proyecto y cierra los FB que vayas a comentar.

#### Generador HMI

Configura los menús de las HMI de un PLC y genera lo que hay que importar en TIA. Todo se edita en la herramienta y se guarda en un archivo de proyecto `.hmimenus` (Nuevo / Abrir / Guardar / Guardar como), que sirve para pasar la configuración de un equipo a otro. Al arrancar se abre el último proyecto usado.

- **HMI**: de 1 a 8. El número de cada una elige su IDB (`HMIMenusConfigNN_IDB`).
- **Pantallas**: por cada subgrupo del menú (OEM 1–8, Installation, Equipment, Manual operations, Profinet diagnostics, Web server, Production data y Energy measurement), el nombre de cada pantalla, común a todas las HMI, y una casilla por HMI para elegir si se ve. En los grupos OEM también se pone el título del grupo. Pulsando el nombre de una HMI en la cabecera se marcan o desmarcan todas.
- **Funciones de usuario**: 24 pantallas con 8 funciones cada una, cada función con nombre, acción izquierda y derecha y feedback izquierda y derecha.
- **Production data**: cada posición de Process counters, Shift counters, Cycle times y Limit values es un dataset con su lista `SO_ProdData_…_Dataset_NN` de 20 o 10 filas, que se escriben en su pestaña.
- **Idiomas**: cada texto se escribe una vez y sale igual en es-ES, en-US y de-DE. Eligiendo «Inglés» o «Alemán» arriba se puede poner un texto distinto en ese idioma; **Traducir** los rellena con un traductor en línea para revisarlos.

**Generar FC y listas** escribe en la carpeta que se elija `CallHMIMenusConfig_FC.xml` (FC 1450, con todas las HMI) y `Listas de textos/*.xml` (las 14 listas de menús y funciones y las 32 de Production data), listos para importar con Importar / Exportar. Las listas llevan siempre todas las entradas: una pantalla sin nombre lleva el texto estándar de su lista (`<<Robot 03>>`…), y una función de usuario sin ningún texto, los suyos (`<<S1 L2 Name>>`…). La especificación está en [`docs/generador-hmi.md`](docs/generador-hmi.md).

## Compilar

Abrir `HerramientasSICAR.sln` en Visual Studio o `dotnet build HerramientasSICAR.sln -c Release`. El resultado es un único `HerramientasSICAR.exe` de 64 bits (las librerías, también las nativas de dibujo, van dentro con Costura, que las extrae a una carpeta temporal al arrancar). `Siemens.Engineering.dll` no se incluye: al arrancar se carga la de la instalación de TIA Portal V19 (ruta del registro de Openness o, si no está, la estándar). Ver `Program.cs`.

## Comprobar antes de un commit

`./checks.sh` compila en Release con los avisos como errores y pasa los tests (`HerramientasSICAR.Tests`, solo la lógica que no necesita TIA, y que todos los textos tengan traducción al inglés). Termina en 0 solo si todo está bien. Necesita TIA Portal V19 instalado para compilar.

## Estructura

El código de la app está en `src/HerramientasSICAR/` y los tests en `tests/HerramientasSICAR.Tests/`. Dentro de `src/HerramientasSICAR/`:

- `Services/TiaOpennessService.cs`: todo lo que habla con TIA (proyectos abiertos, conexión, árbol de bloques, exportar e importar, y las operaciones de cada herramienta en `ProyectoTia`). Va siempre por `HiloOpenness`, un único hilo para las llamadas a Openness.
- `Helpers/`: la lógica de cada herramienta (Excel de DiagExpected, traductor en línea…). `Logica/`: reglas de archivos de bloque, `sincro.json` de Sincro Checker, comentarios de los FB de secuencia del Comentador (`ComentariosSecuencia.cs`), comprobación de versión y, en `Logica/MenusHmi/`, el catálogo de menús, el proyecto `.hmimenus` y la generación del FC y las listas del Generador HMI.
- `Nucleo/`: copia del núcleo de GST-Diag (`src/GstDiag.Core`: Lad, Logic, SimaticMl, Sicar, Symbols, Programs y `Diagnostics/ValueSource.cs`) con lo que lee el programa exportado y comprueba la doble sincronización. Es el mismo código con el espacio de nombres `HerramientasSICAR.Nucleo` y los cambios mínimos para .NET Framework 4.8: `GeneratedRegex` pasa a `Regex` normal, unos pocos métodos que 4.8 no tiene (`Compat.cs`) y PolySharp para lo que le falta al lenguaje. Sus tests son los de GST-Diag (`tests/HerramientasSICAR.Tests/Nucleo/`). Si se cambia la sincro en GST-Diag, se vuelve a copiar.
- `ViewModels/`, `Views/`: la ventana, con Avalonia 11.3 (la última versión que funciona en .NET Framework 4.8, que es lo que pide Openness) y CommunityToolkit.Mvvm. Cada herramienta es un `Herramienta` con su vista; `Contexto` es lo que comparten (conexión, diálogos y barra de estado). `Views/Theme.axaml` tiene los colores y estilos comunes de nuestras apps; `Controls/Icon.cs`, los iconos (Lucide).
- `L.cs` y `Textos.en.cs`: los textos se escriben en castellano y se traducen al inglés con `L.T`. Al añadir un texto, añadir su traducción (el test lo comprueba).

## Licencia

El código se publica con licencia [MIT](LICENSE) © 2026 Ignacio Suarez. Las librerías de terceros que van dentro del
ejecutable tienen sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

La licencia no cubre el logotipo de Alomatek ni las marcas de terceros (Siemens, TIA Portal, Openness…), que pertenecen a sus dueños.
