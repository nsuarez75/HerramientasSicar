# Pendientes

Los errores y peticiones nuevas van como issues de GitHub. Aquí, lo que queda abierto sin issue.

## TIA-Connector (issue #43)

- Probar con TIA todo lo que pasa ahora por TIA-Connector: buscar proyectos y conectar (aviso de permiso de TIA),
  leer el árbol de PLC y HMI, Importar / Exportar (bloques y HMI), Generador HMI «Importar en TIA…», Comentador,
  Numerador, Renumerar Array, DiagExpected y Sincro Checker «Exportar el PLC desde TIA y comprobar».
- Comprobar que al cerrar la app **después de conectar** ya no sale el cierre con error de Windows (`0xc0020001`) y
  que TIA sigue abierto. Sin conectar ya está comprobado (12/11/2026): se cierra sin error y el puente termina.

## .NET 10 y Avalonia 12 (issue #46)

- Probado (12/11/2026): arranca, se ve bien y busca los proyectos abiertos a través del puente.
- Falta repasar a mano cada herramienta con Avalonia 12: diálogos de archivo y carpeta, árbol con casillas, Generador
  HMI (textos con marca de agua, ahora `PlaceholderText`), traductor en línea, tema claro/oscuro e idioma.

## Versión 2.0 (issue #6)

- Los botones que solo llevan icono no tienen nombre accesible (lectores de pantalla, automatización):
  ponerles `AutomationProperties.Name`.

## Importar / Exportar HMI (issue #10)

- Probar desde la app, con un proyecto real, la importación en una HMI de **tablas de variables, scripts, plantillas,
  pop-ups y pantallas** (ciclos y listas ya se probaron). En la primera prueba TIA se cerró al reimportar las tablas
  de variables: falló una con una variable enlazada a un tipo de datos del PLC y, justo después, al sobrescribir la
  tabla estándar. Ahora la tabla estándar no se sobrescribe nunca; falta ver si con eso basta.
- Exportar una HMI entera tarda (unos 6 minutos con ~1000 elementos).

## Sincro Checker (issue #21)

- Usa la comprobación de GST-Diag, copiada en `Nucleo/`. Probado con un programa real y desde la app con TIA.
- GST-Diag ya no tiene doble sincronización (GST-Diag #103): el núcleo copiado en `Nucleo/` es ahora el único y se mantiene aquí.
- Límites (los mismos que GST-Diag): las señales de fuera del PLC son libres y las redes en STL y SCL no se leen.
