# Generador HMI

Especificación acordada con el usuario el 10/11/2026 (issue #23). Sustituye al generador que leía el Excel de
configuración de pantallas HMI de GESTAMP y generaba otro Excel.

## Qué hace

Configura en la propia herramienta los menús de las HMI de un PLC SICAR@GST y genera, en XML de Openness V19, lo
que se importa en TIA con Importar / Exportar:

- el FC `CallHMIMenusConfig_FC` (número 1450), que decide qué pantallas se ven en cada HMI;
- las listas de textos con los nombres de las pantallas y los textos de las funciones de usuario.

Ya no se lee ni se genera ningún Excel.

## Proyecto

- Se guarda en un archivo `.hmimenus` (JSON): Nuevo / Abrir / Guardar / Guardar como. Es la forma de exportar e
  importar la configuración, como los proyectos de GST-LoS y GST-FIT. Al arrancar se abre el último proyecto usado.
- **HMI**: de 1 a 8, cada una con nombre libre y número; el número elige su IDB `HMIMenusConfigNN_IDB`. No puede
  haber dos con el mismo número.
- Los **nombres** de pantallas, los **títulos OEM** y las **funciones de usuario** son comunes a todas las HMI. Lo
  único que cambia por HMI es qué pantallas se ven.

## Menús

Los que ya generaba el generador anterior, más Profinet diagnostics y Energy measurement (antes eran líneas fijas
del FC) y Production data (añadido el 10/11/2026):

| Subgrupo | Posiciones en el FC | Nombres | Lista de textos |
|---|---|---|---|
| OEM 1–8 | 8 | 8 + título del grupo | `CO_OEM_Screens_ID` (título en N0, pantallas en N1–N8) |
| Installation › Layout general | 8 | 8 | `CO_Layout_General_ID` |
| Installation › Layout detailed 1 / 2 | 32 / 32 | 32 / 32 | `CO_Layout_Detailed_ID` (1–32 / 33–64) |
| Installation › Layout fixture | 32 | 32 | `CO_Layout_Fixture_ID` |
| Installation › Traceability | 24 | 24 | `CO_Traceability_ID` |
| Installation › Interface signals | 32 | 32 | `CO_Interface_Signals_ID` |
| Equipments › Robots | 32 | 24 | `CO_Rob_ID` |
| Equipments › Drives, Welding | 32 | — | — |
| Manual operation › Manual movements 1 / 2 | 32 / 32 | 32 / 32 | `CO_Manual_Movements_ID` (1–32 / 33–64) |
| Manual operation › User functions | 24 | 24 | `CO_UserFunctions_ID` (25–32 con el texto estándar) |
| Manual operation › Poka-Yoke test, Docking stations | 8 | 8 | `CO_PokaYokeTest_ID`, `CO_DockingStation_ID` |
| Hardware diagnostics › Profinet diagnostic | 8 | 8 | `CO_ProfinetDiagnose_ID` |
| Hardware diagnostics › Web server | — | 8 | `CO_WebServer_Device_ID` |
| Production data › Process counters, Shift counters, Cycle times, Limit values | 8 | — | ver abajo |
| System › Energy | 7 | — | — |

Process diagnostics y el resto de System no se tocan.

### Production data

Cada posición de los cuatro subgrupos es un **dataset**, con su propia lista
`SO_ProdData_<ProcessCounter|ShiftCounter|CycleTime|LimitValue>_Dataset_NN`: 20 filas en contadores de proceso y
tiempos de ciclo, 10 en contadores de turno y valores límite (por ejemplo, Cycle times 1 lleva un robot por fila).
Son listas SO del estándar, pero se modifican igual que las CO. Se editan en la pestaña Production data; el
ocultamiento, en Pantallas como los demás (visibles por defecto). Un dataset sin ningún texto lleva los estándar
(`<<Tiempos de ciclo, imagen 05-datos 02>>`); si tiene alguno, lo vacío sale vacío.

## Ocultamiento (FC)

- Cada posición de cada subgrupo tiene una casilla por HMI, independiente de que tenga nombre. Marcada = se ve
  (`alwaysFalse`, log. 0); sin marcar = oculta (`alwaysTrue`, log. 1).
- Una HMI nueva empieza con todo visible salvo Profinet 8 y Energy 2–7, que el FC de la plantilla ocultaba siempre.
- El FC lleva siempre todas las HMI, en orden de número, con tres segmentos cada una: título `HMI N`, configuración
  en AWL y llamada LAD a `HMIMenusConfig_FB` con su IDB. Así, al importarlo no se pisa ninguna HMI.

## Listas de textos

- Siempre completas: todas las entradas, se vean o no las pantallas (la visibilidad solo afecta al FC).
- Un texto por entrada que va igual a es-ES, en-US y de-DE. Se puede poner un texto distinto en inglés o alemán, y
  **Traducir** los rellena con un traductor en línea para revisarlos.
- Pantalla sin nombre: el texto estándar de su lista en cada idioma (`<<Layout nivel general 07>>`,
  `<<Layout general level 07>>`…). La entrada 0 lleva siempre el estándar con `xx`.
- Título OEM vacío: `OEM_0N`.
- `CO_UserFunctions`: 5 entradas por función (pantalla P, función F → valores P·100 + F·10 + 1 a 5, desde 111):
  nombre, acción izquierda, acción derecha, feedback izquierda y feedback derecha. Si la función no tiene ningún
  texto, lleva los estándar (`<<S1 L2 Name>>`…); si tiene alguno, lo vacío sale vacío.

## Generar

Siempre todo, sin elegir HMI ni componentes, en la carpeta que se elija:

```
<carpeta>/CallHMIMenusConfig_FC.xml
<carpeta>/Listas de textos/<lista>.xml   (46 listas: 14 de menús y funciones + 32 de Production data)
```

## Importar en TIA (añadido el 10/11/2026, issue #31)

Pestaña «Importar en TIA» (botón «Importar en TIA…» de la barra), con el proyecto conectado:

- Qué importar, con casillas y «Todas / Ninguna» por grupo: el FC `CallHMIMenusConfig_FC`, las 14 listas de menús
  y funciones, las 32 listas de Production data.
- PLC del FC (uno de los PLC del proyecto) y HMI de las listas (varias: las listas son iguales para todas).
- Se genera en una carpeta temporal, se importa y se borra. Si el PLC ya tiene el FC, se sobrescribe en su carpeta;
  si no, se crea en `08_HMI_Management › 01_HMI_Configuration`. Las listas van a la raíz de «Listas de textos» de
  cada HMI y sobrescriben las que tengan el mismo nombre.

## Comprobado

Con el Excel de un proyecto real convertido a `.hmimenus` (una sola vez, fuera del repo), el FC generado coincide con
el de TIA en todas las HMI salvo lo que el Excel tenía distinto del FC importado y Profinet/Energy, que ahora se
escriben explícitamente con el mismo efecto.
