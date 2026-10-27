# Pendientes

Los errores y peticiones nuevas van como issues de GitHub. Aquí, lo que queda abierto sin issue.

## Sincro Checker

- Revisar con el usuario qué **coincidencias** son reales en un proyecto de verdad y si hacen falta más reglas
  físicas para robots.
- Limitaciones conocidas:
  - Los **segmentos** en STL o SCL no se interpretan; solo LAD.
  - Las bobinas S/R sobre `ilockAuto` no se modelan: cuenta la última bobina `=`.
  - Los contactos que leen variables escritas antes en el mismo ciclo se tratan como señales libres, igual que
    temporizadores, comparadores y flancos.
