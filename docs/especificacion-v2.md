# Especificación de la versión 2.0

Acordada con el usuario el 27/10/2026 (issue #6): pasar la interfaz al estándar común de nuestras apps e integrar ImportExportTIA.

## Lo que se ve

- **Conexión común con TIA.** En la barra superior se elige un proyecto ya abierto en TIA Portal V19 y se pulsa Conectar. DiagExpected, Numerador, Renumerar Array, Sincro Checker e Importar / Exportar trabajan sobre ese proyecto: ya no piden el `.ap19`.
- **La app nunca abre, guarda ni cierra proyectos.** Solo aparecen los proyectos que ya están abiertos en TIA.
- **Sin conexión**, cada herramienta con TIA muestra el aviso «Conecta con un proyecto de TIA en la barra superior» y sus botones están desactivados. Sincro Checker sigue pudiendo abrir un XML ya exportado.
- **Lateral en dos grupos**: «Con TIA» (Importar / Exportar, DiagExpected, Numerador, Renumerar Array, Sincro Checker) y «Con archivos» (Comentador, Generador HMI).
- **Importar / Exportar**: el árbol de bloques va dentro de su pantalla, no en el lateral. Funciona igual que ImportExportTIA.
- **Barra superior**: «Herramientas SICAR» con el subtítulo «Utilidades para proyectos SICAR@GST en TIA Portal»; idioma, tema y Acerca de a la derecha.
- **Inglés**: toda la interfaz traducida; en inglés la app se llama «SicarTools». Lo que generan las herramientas (Excel de DiagExpected, comentarios ES/EN/DE, textos del HMI) no depende del idioma de la ventana.
- **Sin pie de ventana**: versión y autor solo en Acerca de. El enlace de Buy Me a Coffee sale de la app y pasa a un botón en el README del repo índice GST-Tools.

## Cómo se hace

- Avalonia 11.3 con el estándar de interfaz (referencia: ImportExportTIA), .NET Framework 4.8 de 64 bits, solo Openness V19.
- La lógica de cada herramienta se mantiene; cambian las pantallas, los diálogos y la conexión.
- Un único servicio de Openness, tomado de ImportExportTIA, con todas las llamadas en un hilo propio.
- Versión 2.0.0.

## Después de publicarla

- Cuando el usuario haya probado la v2.0.0, se borra el repo ImportExportTIA (pidiendo confirmación justo antes) y se quita del índice GST-Tools.
