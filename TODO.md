# Pendientes

Los errores y peticiones nuevas van como issues de GitHub. Aquí, lo que queda abierto sin issue.

## Versión 2.0 (issue #6)

- Probar con TIA cada herramienta del grupo «Con TIA» sobre el **proyecto conectado**: DiagExpected, Numerador, Renumerar Array y Sincro Checker (exportar desde TIA).
- Los botones que solo llevan icono no tienen nombre accesible (lectores de pantalla, automatización):
  ponerles `AutomationProperties.Name`.

## Importar / Exportar HMI (issue #10)

- Probar desde la app, con un proyecto real, la importación en una HMI de **tablas de variables, scripts, plantillas,
  pop-ups y pantallas** (ciclos y listas ya se probaron). En la primera prueba TIA se cerró al reimportar las tablas
  de variables: falló una con una variable enlazada a un tipo de datos del PLC y, justo después, al sobrescribir la
  tabla estándar. Ahora la tabla estándar no se sobrescribe nunca; falta ver si con eso basta.
- Exportar una HMI entera tarda (unos 6 minutos con ~1000 elementos).

## Sincro Checker (issue #21)

- Usa la comprobación de GST-Diag, copiada en `Nucleo/`. Probado con un programa real: los mismos pares por
  secuencia que GST-Diag. Falta probar desde la app «Exportar el PLC desde TIA y comprobar» con el proyecto
  conectado, y marcar y borrar señales excluyentes.
- Cuando cambie la doble sincronización en GST-Diag, volver a copiar su núcleo (ver README, Estructura).
- Límites (los mismos que GST-Diag): las señales de fuera del PLC son libres y las redes en STL y SCL no se leen.

## Generador HMI (issue #23)

- Probar en TIA la importación de `CallHMIMenusConfig_FC.xml` y de las listas de textos generadas.
