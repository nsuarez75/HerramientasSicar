# HerramientasSICAR

Herramientas que automatizan tareas repetitivas en los proyectos TIA Portal de líneas de GESTAMP programadas con el
estándar **SICAR@GST**. El vocabulario común a todas las apps está en el `GLOSSARY.md` del repo GST-Tools.

## Estándar SICAR

**SICAR@GST**:
Estándar de programación de PLC de GESTAMP para las líneas de producción. Esta app trabaja con la versión V4.

**FB de secuencia**:
Bloque que programa el ciclo de una estación como una lista de **pasos**, uno por **segmento**, más el **PERM**.
_Evitar_: secuencia (a secas), grafcet

**Paso**:
Cada etapa de un **FB de secuencia**. Los automáticos se llaman `S###` (de S001 a S254).
_Evitar_: estado, etapa

**Paso manual**:
**Paso** que solo se usa en modo manual. En un FB de secuencia van todos al final, a partir del primero. Como no
programa `ilockAuto`, nunca es candidato al sincronizar.

**PERM**:
Parte del **FB de secuencia** que se ejecuta en cada ciclo junto con el **paso** activo. Sus `ilockAuto` e
`ilockExtSync` se suman con Y a los de cada paso al sincronizar (manual SICAR 2.2.5); lo que no programa vale 1.

**Segmento**:
Cada bloque de lógica de un FB en TIA Portal. En un **FB de secuencia**, cada **paso** ocupa un segmento cuyo título
describe el paso.
_Evitar_: network (salvo en la interfaz de TIA en inglés)

**Ilock**:
Condición que debe cumplirse para que un **paso** sea válido. `ilockAuto` e `ilockExtSync` son los del modo
automático. Si un paso no los programa, `ilockAuto` vale 0 e `ilockExtSync` vale 1 (manual SICAR 2.3).
_Evitar_: enclavamiento, interlock

**transAuto**:
Condición de transición de un **paso** en automático: a 1, el paso avanza. Un paso con `transAuto` a 1 no se propone
para sincronizar aunque cumpla sus **ilocks**; sin programar vale 0.

**Salto**:
Escritura de un número de **paso** en `#Sequence.stepNplus1`: si se cumple su condición, la secuencia va a ese paso.
Al sincronizar, no se prueban los pasos que se salta un salto cumplido de un paso anterior (manual SICAR 2.9).

**Pulso de sincronización**:
`#Sequence.pos_Pulse_OM_Sync`, que vale 1 durante el scan en que se sincroniza. Se suele poner negado en las
condiciones de **salto** para que no salten durante la sincronización.

**Condiciones de activación**:
Lo que hace valer 1 a una señal que el programa calcula con una sola bobina. Al sincronizar, la señal vale sus
condiciones de activación (hasta 10 niveles hacia atrás); las que tienen SET/RESET o se escriben en varios sitios no.

**Actuador**:
Elemento que se mueve en la estación (cilindro, pinza…), con dos posiciones: `-H` (reposo) y `-W` (trabajo). Nunca
está en las dos a la vez.

**Señal decodificada**:
Señal de la que solo un valor puede estar activo a la vez, como `ProcessCode=N` o una comparación `x == N`.

**FB de mensajes**:
Bloque que agrupa los mensajes de alarma de una estación. Se numeran antes de generar el **DiagAddon**.

**DiagAddon**:
Diagnóstico de SICAR que se genera a partir de los **FB de mensajes** del proyecto.

## Herramientas

**Error de sincronización**:
Al pasar de manual a automático, el motor SICAR salta al **paso** cuyos **ilocks** se cumplen. Si se cumplen en
varios a la vez, da error de sincronización.

**Coincidencia**:
Par de **pasos** para los que existe un estado de señales que cumple los **ilocks** de los dos: es un posible
**error de sincronización**, que detecta Sincro Checker. Se calcula igual que la doble sincronización de GST-Diag.
_Evitar_: conflicto, solape

**Señales excluyentes**:
Señales que el usuario marca porque nunca están a 1 a la vez (por ejemplo, las prioridades que elige un bloque
llamado). Sincro Checker las tiene en cuenta y las guarda en `sincro.json` junto al programa exportado.

**Plantilla HMI**:
Proyecto HMI estándar que suministra GESTAMP y que el Generador HMI rellena con los menús de cada estación.

**Proyecto conectado**:
Proyecto abierto en TIA Portal que se ha elegido en la barra superior. Las herramientas del grupo «Con TIA» trabajan
sobre él; la app nunca lo abre, guarda ni cierra.
_Evitar_: proyecto seleccionado, ruta del proyecto

**Elemento**:
Cualquier cosa que se marca en el árbol de Importar / Exportar y se guarda como un XML: un bloque de un PLC o un
elemento de una **HMI** (pantalla, tabla de variables, lista de textos, script…).
_Evitar_: objeto, ítem

**Rama (de HMI)**:
Cada apartado de una **HMI** en el árbol de Importar / Exportar: Pantallas, Plantillas, Pop-ups, Slide-ins, Variables
HMI, Conexiones, Ciclos, Listas de textos, Listas de gráficos y Scripts VB, más los dos **elementos** únicos
(Elementos globales y Vista general de pantallas). Al exportar, cada rama es una subcarpeta; al importar, cada archivo
va a la suya.

**HMI**:
En Importar / Exportar, un panel o estación PC con WinCC clásico (Basic, Comfort o Advanced). Las HMI Unified no se
muestran.

## Generador HMI

**Menú (de HMI)**:
Árbol de pantallas de la HMI SICAR: **grupos** (OEM, Installation, Equipments…) con **subgrupos** (Layout general,
Robots…), y en cada subgrupo varias **posiciones**, una por pantalla.
_Evitar_: imagen (para la pantalla), submenú

**Ocultamiento**:
Si una **posición** del menú se ve o no en una HMI. Lo escribe el FC `CallHMIMenusConfig_FC` en
`HMIMenusConfigNN_IDB.statCfg.Blanking`: log. 0 = se ve, log. 1 = oculta. No cambia las **listas de textos**.
_Evitar_: blanking (salvo en el PLC), visibilidad de imágenes

**Lista de textos**:
Lista de WinCC que da el texto de cada valor (por ejemplo, el nombre de cada pantalla). El Generador HMI la genera
completa en es-ES, en-US y de-DE.
_Evitar_: text list (salvo en la interfaz de TIA en inglés)

**Texto estándar**:
Texto `<<…>>` que lleva una entrada de **lista de textos** sin nombre, el mismo que trae la plantilla de GESTAMP
(`<<Robot 03>>`, `<<S1 L2 Name>>`).
_Evitar_: placeholder, texto por defecto

**Función de usuario**:
Botón de una pantalla de User functions de la HMI (24 pantallas × 8 funciones) con cinco textos: nombre, acción
izquierda y derecha y feedback izquierda y derecha.

**Dataset (de Production data)**:
Cada posición de los subgrupos de Production data (contadores, tiempos de ciclo, valores límite). Tiene su lista de
textos `SO_ProdData_…_Dataset_NN`, con una fila por elemento de la pantalla.
