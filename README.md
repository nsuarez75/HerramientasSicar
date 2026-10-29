# Herramientas SICAR

Conjunto de herramientas para automatizar algunas tareas en los proyectos de líneas de producción de GESTAMP basadas en el estándar SICAR@GST V4. En inglés, la app se llama **SicarTools**.

## Requisitos

- Windows con .NET Framework 4.8.
- TIA Portal V19 instalado, con Openness. Las herramientas que trabajan con archivos (Comentador y Generador HMI) funcionan sin él.
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
Sincro Checker detecta esa situación al programar: abre un FB de secuencia (LAD) y comprueba, para cada par de pasos, si existe algún estado de señales que cumpla los ilocks de ambos.

- Se puede abrir un XML ya exportado (no necesita conexión) o exportar el bloque desde el proyecto conectado.
- Se comprueban todos los pasos automáticos `S###`, cualquier combinación de dos (si tres pasos pudieran coincidir, también lo haría cada par).
- Condición de cada paso: `ilockAuto` e `ilockExtSync` del paso, con `transAuto` y sus saltos a 0. Un paso con `transAuto` a 1, o con la condición de un salto cumplida (MOVE de un número de paso a `#Sequence.stepNplus1`), no se propone para sincronizar aunque cumpla sus ilocks, porque puede avanzar. Todo se evalúa en el scan de sincronización, con `#Sequence.pos_Pulse_OM_Sync` a 1 (los saltos que lo llevan negado no cuentan). Las condiciones de PERM no afectan a la sincronización y no se tienen en cuenta. Un ilock que la network no escribe vale 1; un `transAuto` que no escribe, 0.
- Los pasos manuales se omiten: desde el primer paso manual en adelante todos se consideran manuales. Se detecta como el primer paso que solo escribe `ilockManual`/`limitManual`; si no se detecta, la herramienta pide indicarlo (vacío = no hay pasos manuales).
- Casilla «Secuencia de robot»: en las secuencias de robot no se comprueban los pasos S223 a S227 (rutinas de servicio, vuelta a casa…), que por cómo funciona el robot no pueden coincidir con los demás. En las de utillaje se deja sin marcar.
- Reglas físicas opcionales: un actuador no puede estar en `-H` y `-W` a la vez, y solo un valor puede estar activo en señales decodificadas (`ProcessCode=N`, comparaciones `x == N`).
- Cada coincidencia se muestra en una vista LAD interactiva con los dos pasos, cargada con un estado de ejemplo que provoca el error. Al hacer clic en un contacto se cambia su señal y se ve en vivo qué pasos sincronizan.
- Para cada coincidencia muestra las diferencias entre los dos pasos: señales que un paso exige siempre y el otro no comprueba (o admite con el valor contrario). Son hechos sobre los ilocks, no propuestas: qué señal usar para separarlos depende del proceso. Esas señales se resaltan en el LAD.

### Con archivos

#### Comentador

Genera el comentario de los pasos de secuencia en castellano, inglés y alemán de manera automática, partiendo del título del segmento.

- Exportar los textos del proyecto (seleccionar únicamente "block comment").
<img width="221" height="145" alt="image" src="https://github.com/user-attachments/assets/ede494a7-db1b-42a5-9757-3196a9f33fe9" />

<img width="644" height="697" alt="image" src="https://github.com/user-attachments/assets/66866a69-a1a1-422d-aadb-7123a28c41c6" />

- Elegir el archivo de origen, poner nombre al archivo de salida y pulsar **Generar comentarios**.
- Si se marca la traducción automática se necesita conexión a internet. La traducción usa servicios de terceros (Google Translate), y su calidad depende de la del título de cada paso de la secuencia.
- Importar de vuelta los textos generados. Es importante no tener ningún FB de secuencia abierto durante la importación. Marcar "Import source language".
<img width="502" height="165" alt="image" src="https://github.com/user-attachments/assets/de829597-2f9f-44e3-904e-2bde345b5b46" />

#### Generador HMI

Partiendo de una plantilla estándar suministrada por GESTAMP, rellena la configuración de menús de cada HMI del proyecto con el nombre de cada menú/submenú y genera un Excel con todas las listas de textos completadas para copiar en el proyecto, así como el código AWL de configuración de visibilidad de las imágenes del HMI.

## Compilar

Abrir `HerramientasSICAR.sln` en Visual Studio o `dotnet build HerramientasSICAR.sln -c Release`. El resultado es un único `HerramientasSICAR.exe` de 64 bits (las librerías, también las nativas de dibujo, van dentro con Costura, que las extrae a una carpeta temporal al arrancar). `Siemens.Engineering.dll` no se incluye: al arrancar se carga la de la instalación de TIA Portal V19 (ruta del registro de Openness o, si no está, la estándar). Ver `Program.cs`.

## Comprobar antes de un commit

`./checks.sh` compila en Release con los avisos como errores y pasa los tests (`HerramientasSICAR.Tests`, solo la lógica que no necesita TIA, y que todos los textos tengan traducción al inglés). Termina en 0 solo si todo está bien. Necesita TIA Portal V19 instalado para compilar.

## Estructura

El código de la app está en `src/HerramientasSICAR/` y los tests en `tests/HerramientasSICAR.Tests/`. Dentro de `src/HerramientasSICAR/`:

- `Services/TiaOpennessService.cs`: todo lo que habla con TIA (proyectos abiertos, conexión, árbol de bloques, exportar e importar, y las operaciones de cada herramienta en `ProyectoTia`). Va siempre por `HiloOpenness`, un único hilo para las llamadas a Openness.
- `Helpers/`: la lógica de cada herramienta (análisis LAD de Sincro Checker, Excel de DiagExpected, Comentador…). `Logica/`: reglas de archivos de bloque y comprobación de versión.
- `ViewModels/`, `Views/`: la ventana, con Avalonia 11.3 (la última versión que funciona en .NET Framework 4.8, que es lo que pide Openness) y CommunityToolkit.Mvvm. Cada herramienta es un `Herramienta` con su vista; `Contexto` es lo que comparten (conexión, diálogos y barra de estado). `Views/Theme.axaml` tiene los colores y estilos comunes de nuestras apps; `Controls/Icon.cs`, los iconos (Lucide); `Controls/LadRungView.cs`, el dibujo LAD de Sincro Checker.
- `L.cs` y `Textos.en.cs`: los textos se escriben en castellano y se traducen al inglés con `L.T`. Al añadir un texto, añadir su traducción (el test lo comprueba).

## Licencia

El código se publica con licencia [MIT](LICENSE) © 2026 Ignacio Suarez. Las librerías de terceros que van dentro del
ejecutable tienen sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

La licencia no cubre el logotipo de Alomatek ni las marcas de terceros (Siemens, TIA Portal, Openness…), que pertenecen a sus dueños.
