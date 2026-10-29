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
**Paso** que solo se usa en modo manual. En un FB de secuencia van todos al final, a partir del primero.

**PERM**:
Parte del **FB de secuencia** que se ejecuta en cada ciclo junto con el **paso** activo. Sus condiciones no
intervienen en la sincronización.

**Segmento**:
Cada bloque de lógica de un FB en TIA Portal. En un **FB de secuencia**, cada **paso** ocupa un segmento cuyo título
describe el paso.
_Evitar_: network (salvo en la interfaz de TIA en inglés)

**Ilock**:
Condición que debe cumplirse para que un **paso** sea válido. `ilockAuto` e `ilockExtSync` son los del modo
automático; si un paso no los escribe, valen 1.
_Evitar_: enclavamiento, interlock

**transAuto**:
Condición de transición de un **paso** en automático: a 1, el paso avanza. Un paso con `transAuto` a 1 no se propone
para sincronizar aunque cumpla sus **ilocks**.

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
**error de sincronización**, que detecta Sincro Checker.
_Evitar_: conflicto, solape

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
