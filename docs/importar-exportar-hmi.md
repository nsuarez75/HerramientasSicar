# Importar / Exportar: HMI

Especificación de la issue #10, acordada con el usuario en entrevista (2026-10-27).

## Qué hace

- En el árbol de Importar / Exportar aparecen, junto a los PLC, las **HMI WinCC clásicas** del proyecto conectado
  (paneles Basic/Comfort y estaciones PC con WinCC RT Advanced). Las HMI **Unified** no se muestran: Openness V19 solo
  deja sacar sus variables y scripts.
- Cada HMI se despliega en **ramas**, en el orden de TIA: Pantallas, Plantillas, Pop-ups, Slide-ins, Elementos
  globales, Vista general de pantallas, Variables HMI, Conexiones, Ciclos, Listas de textos, Listas de gráficos y
  Scripts VB. Pantallas, Plantillas, Pop-ups, Variables HMI y Scripts VB conservan sus carpetas de TIA. Elementos
  globales y Vista general son un único elemento cada uno y cuelgan directamente de la HMI.
- Las variables se marcan por **tablas enteras**; lo demás, elemento a elemento. Marcar una rama o una carpeta marca
  todo lo que tiene dentro. El filtro de texto busca también en las HMI.
- Se pueden marcar a la vez bloques y elementos de HMI y **exportarlo todo de una vez**.

## Exportar

Cada elemento se guarda como `<nombre>.xml` en `<carpeta elegida>\[<dispositivo>\]<rama>\<carpetas de TIA>\`.
La subcarpeta del dispositivo solo aparece si lo marcado es de más de un PLC o HMI. Los nombres de las carpetas de rama
están siempre en castellano (no cambian con el idioma de la interfaz), porque la importación los reconoce.

Las carpetas de las HMI suelen ser profundas y con nombres largos. Si la ruta de un archivo pasa del límite de Windows
(260 caracteres), ese elemento sale en la lista de fallos con un aviso claro: hay que exportar a una carpeta de ruta
más corta (por ejemplo, `C:\Export`). Exportar una HMI entera lleva su tiempo: TIA tarda alrededor de un segundo por
elemento.

## Importar

- Se selecciona en el árbol **cualquier punto de la HMI** destino. Cada archivo va a **su rama**, según el tipo que
  dice su XML. Si lo seleccionado es una carpeta de la misma rama que el archivo, entra en esa carpeta; si no, va a la
  raíz de su rama.
- Con «Importar carpeta», las subcarpetas se crean como carpetas de TIA. Si la ruta pasa por una carpeta con el nombre
  de la rama (como la deja Exportar), solo cuentan las carpetas que hay después.
- Orden de importación, para que nada quede con referencias rotas: conexiones, ciclos, listas de textos, listas de
  gráficos, variables, scripts, plantillas, elementos globales, pop-ups, slide-ins, pantallas y vista general.
- Lo que ya existe con el mismo nombre **se sobrescribe**, tras confirmarlo, igual que con los bloques.
- La **tabla de variables estándar** de la HMI destino no se sobrescribe nunca: sale en la lista de fallos. En la
  primera prueba con un proyecto real, TIA se cerró al sobrescribirla (justo después de fallar otra tabla con una
  variable enlazada a un tipo de datos del PLC).
- Si TIA se cierra a mitad, la importación (o exportación) se para y dice cuántos elementos quedaban.
- Los archivos que no encajan con el dispositivo seleccionado (un bloque con una HMI seleccionada, una pantalla con un
  PLC) **se descartan** y salen en la lista de fallos.

## Fuera de alcance

- **Avisos, recetas y administración de usuarios**: Openness V19 no deja exportarlos ni importarlos.
- **Gráficos**: en TIA son del proyecto, no de cada HMI (carpeta «Idiomas y recursos»), así que no están en el árbol de
  la HMI.
- **HMI Unified** (ver arriba).
