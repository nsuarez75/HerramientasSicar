# Pendientes

Los errores y peticiones nuevas van como issues de GitHub. Aquí, lo que queda abierto sin issue.

## Versión 2.0 (issue #6)

- Probar con TIA cada herramienta del grupo «Con TIA» sobre el **proyecto conectado**: Importar / Exportar,
  DiagExpected, Numerador, Renumerar Array y Sincro Checker (exportar desde TIA).
- Probar Sincro Checker con un FB de secuencia real: la vista LAD se ha redibujado para Avalonia.
- Tras probar y publicar la v2.0.0: borrar el repo ImportExportTIA (confirmándolo antes con el usuario) y quitarlo
  del índice GST-Tools.
- Los botones que solo llevan icono no tienen nombre accesible (lectores de pantalla, automatización):
  ponerles `AutomationProperties.Name`.

## Sincro Checker

- Revisar con el usuario qué **coincidencias** son reales en un proyecto de verdad y si hacen falta más reglas
  físicas para robots.
- Limitaciones conocidas:
  - Los **segmentos** en STL o SCL no se interpretan; solo LAD.
  - Las bobinas S/R sobre `ilockAuto` no se modelan: cuenta la última bobina `=`.
  - Los contactos que leen variables escritas antes en el mismo ciclo se tratan como señales libres, igual que
    temporizadores, comparadores y flancos.
