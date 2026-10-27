# Pendientes

Los errores y peticiones nuevas van como issues de GitHub. Aquí, lo que queda abierto sin issue.

## Versión 2.0 (issue #6)

- Probar con TIA cada herramienta del grupo «Con TIA» sobre el **proyecto conectado**: DiagExpected, Numerador, Renumerar Array y Sincro Checker (exportar desde TIA).
- Probar Sincro Checker con un FB de secuencia real: la vista LAD se ha redibujado para Avalonia.
- Los botones que solo llevan icono no tienen nombre accesible (lectores de pantalla, automatización):
  ponerles `AutomationProperties.Name`.

## Importar / Exportar HMI (issue #10)

- Probar desde la app, con un proyecto real, la importación en una HMI de **tablas de variables, scripts, plantillas,
  pop-ups y pantallas** (ciclos y listas ya se probaron). En la primera prueba TIA se cerró al reimportar las tablas
  de variables: falló una con una variable enlazada a un tipo de datos del PLC y, justo después, al sobrescribir la
  tabla estándar. Ahora la tabla estándar no se sobrescribe nunca; falta ver si con eso basta.
- Exportar una HMI entera tarda (unos 6 minutos con ~1000 elementos).

## Sincro Checker

- Revisar con el usuario qué **coincidencias** son reales en un proyecto de verdad y si hacen falta más reglas
  físicas para robots.
- Limitaciones conocidas:
  - Los **segmentos** en STL o SCL no se interpretan; solo LAD.
  - Las bobinas S/R sobre `ilockAuto` no se modelan: cuenta la última bobina `=`.
  - Los contactos que leen variables escritas antes en el mismo ciclo se tratan como señales libres, igual que
    temporizadores, comparadores y flancos.
