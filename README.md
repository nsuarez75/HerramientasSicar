## Propósito

Conjunto de herramientas para automatizar algunas tareas en los proyectos de líneas de producción de GESTAMP basadas en el estándar SICAR@GST V4.

## Requisitos

- .net framework 4.8
- Usuario dentro del grupo TIA Openness en windows

## Funcionalidades

Las funcionalidades que solicitan elegir ruta de proyecto, requieren tener TIA abierto con ese proyecto abierto. No más de esa instancia abierta simultaneamente.

### DiagExpected

Dado un FB de secuencia, genera un excel con el estado necesario en cada paso de actuadores y sensores.
Es necesario revisar el contenido generado para evitar errores, ya que no funciona de manera perfecta, sirve para sacar una primera plantilla sobre la que trabajar.

### Numerador

Renumera de 1 a X los mensajes de todos los FB de mensajes del proyecto.
Paso previo a realizar una generación de DiagAddon.

### Comentador

Genera el comentario de los pasos de secuencia en español inglés y alemán de manera automática partiendo del titulo del segmento.

#### Instrucciones

- Exportar textos del proyecto (seleccionar unicamente "block comment").
<img width="221" height="145" alt="image" src="https://github.com/user-attachments/assets/ede494a7-db1b-42a5-9757-3196a9f33fe9" />

<img width="644" height="697" alt="image" src="https://github.com/user-attachments/assets/66866a69-a1a1-422d-aadb-7123a28c41c6" />


- Seleccionar el archivo de origen, elegir nombre para el archivo de salida y hacer click en el botón de procesar.
- Si se selecciona la opción de traducción automática se requiere conexión a internet. Tener en cuenta que la traducción usa APIs de terceros
  como Google Translator, y su calidad se verá limitada a la calidad del titulo de ese paso de la secuencia.
- Importar de vuelta los textos generados. Es importante no tener ningun FB de secuencia abierto durante la importación. Seleccionar el check "Import source language".
<img width="502" height="165" alt="image" src="https://github.com/user-attachments/assets/de829597-2f9f-44e3-904e-2bde345b5b46" />

### Renumerar Array

Esta funcionalidad no es especifica para el estándar SICAR@GST.
Lo que hace es coger una variable Array del tipo que sea en un FB/FC dado y renumerar todas sus apariciones de menor a mayor.
Útil cuando tienes un array de flancos por ejemplo y necesitas intercalar un nuevo segmento sin tener que calcular el último usado o reordenar todos para tener orden.
Si la variable pertenece a un DB Global, no es necesario indicar el nombre anidado con el DB, solamente el nombre de la variable.

### Generador HMI

Partiendo de una plantilla estándar suministrada por GESTAMP, se rellena la configuración de menús de cada HMI del proyecto con el nombre de cada menú/submenú
y se genera una excel con todas las listas de textos completadas para copiar en el proyecto, así como el codigo AWL de configuración de visibilidad de las imagenes del HMI.





### Sincro Checker

Al pasar de manual a automático, el motor de secuencia SICAR busca el paso cuyos ilocks se cumplen y salta a él; si se cumplen en varios pasos a la vez da error de sincronización.
Sincro Checker detecta esa situación al programar: abre un FB de secuencia (LAD) y comprueba, para cada par de pasos, si existe algún estado de señales que cumpla los ilocks de ambos.

- Se comprueban todos los pasos automáticos `S###`, cualquier combinación de dos (si tres pasos pudieran coincidir, también lo haría cada par).
- Condición de cada paso: `ilockAuto` y `ilockExtSync` del paso, junto con los de PERM (que se ejecuta en cada ciclo con el paso activo). Un ilock que la network no escribe vale 1.
- Los pasos manuales se omiten: desde el primer paso manual en adelante todos se consideran manuales. Se detecta como el primer paso que solo escribe `ilockManual`/`limitManual`; si no se detecta, la herramienta pide indicarlo (vacío = no hay pasos manuales).
- La lógica se reconstruye a partir del cableado del bloque (`Wires`), igual que SIMATIC Automation Compare Tool, no del orden de los elementos en el XML.
- Reglas físicas opcionales: un actuador no puede estar en `-H` y `-W` a la vez, y solo un valor puede estar activo en señales decodificadas (`ProcessCode=N`, comparaciones `x == N`).
- Cada coincidencia se muestra en una vista LAD interactiva con PERM y los dos pasos, cargada con un estado de ejemplo que provoca el error. Al hacer clic en un contacto se cambia su señal y se ve en vivo qué pasos sincronizan.
- Para cada coincidencia muestra las diferencias entre los dos pasos: señales que un paso exige siempre y el otro no comprueba (o admite con el valor contrario). Son hechos sobre los ilocks, no propuestas: qué señal usar para separarlos depende del proceso. Esas señales se resaltan en el LAD.
- Si se abre la tabla de variables de una estación (`++XXXX.xml`) en lugar de su FB, propone abrir el FB de secuencia de la misma carpeta.
- Se puede abrir un XML ya exportado o exportar el bloque desde el proyecto TIA abierto.
- Las networks STL/SCL no se interpretan.
