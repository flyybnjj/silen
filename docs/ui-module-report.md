# Silen: módulo de interfaz

## Diseño

Interfaz WPF nativa en español, con barra lateral y seis vistas: Resumen, Hardware, Drivers, Mejoras, Gaming e Historial. Ventana inicial de 1240 × 820 y mínimo de 1000 × 680. Las áreas largas se desplazan y las tablas conservan anchos legibles con desplazamiento horizontal.

Se aplicó la guía `frontend-design`: azul tinta `#101A2B`, panel `#17253B`, texto `#EEF4FC`, texto secundario `#A7B8CC`, acento `#62B6FF` y aviso `#F5BF69`. Segoe UI Variable/Segoe UI, títulos de 29 px y datos de recursos de 38 px. La identidad proviene de trazas instrumentales de muestras reales y filas de hallazgos con evidencia; no hay puntuación de salud ni FPS simulados.

Se revisó la propuesta antes de construir: los recursos comparten un panel, en lugar de repetir tarjetas de igual jerarquía; cada hallazgo explica su medición y su acción. La observación conserva su fecha, los sensores ausentes dicen «No disponible» y los campos vacíos del inventario dicen «Sin dato».

## Archivos

- `App.xaml.cs`: inicio, CLI de escaneo/render/prueba y salida con códigos verificables.
- `MainViewModel.cs`: adquisición, historial, búsqueda de drivers, exportación y sesiones de gaming. Una operación activa impide iniciar otra.
- `OperationLifetime.cs`: cancelación y cierre que espera la limpieza asíncrona, antes de terminar la aplicación.
- `MainWindow.xaml` y `Theme.xaml`: vistas, componentes, foco visible, nombres accesibles y mensajes de estado.
- `Commands.cs`, `DisplayModels.cs`, `ResourceTrace.cs`: comandos, traducciones de estado y trazas de datos adquiridos.

## Comportamiento

El escaneo usa `WindowsCollector`, ejecuta las reglas de Core y guarda su snapshot. Los fallos de guardado no descartan el diagnóstico. El registro de guías conserva la diferencia entre abrir una herramienta y aplicar un cambio. La carga de otra observación elimina las ofertas de drivers previas. La consulta voluntaria de Windows Update muestra ofertas reales, ausencia de ofertas o el error de consulta.

Gaming admite un CSV de PresentMon para la sesión A y otro para B; habilita B después de importar A. Captura mediante un ejecutable de consola elegido, PID y duración de 15–300 s. Conserva el CSV y documenta FPS, frame times, percentiles y 1% low. Las diferencias se calculan sólo cuando las sesiones usan la misma columna de tiempo; el escenario también debe repetirse. Las trazas muestran hasta 180 frames reales seleccionados, sin generar muestras.

Los informes se exportan como JSON con las opciones de Core o Markdown. Junto a la exportación se indica que pueden contener hardware y nombres de procesos. Los documentos y licencias se copian a la distribución.

## CLI

```text
Silen.exe --scan <informe.json>
Silen.exe --render <vista.png>
Silen.exe --smoke-test <directorio>
```

`--scan` realiza un escaneo real y guarda JSON. `--render` obtiene una observación real y renderiza la vista Resumen al completar el layout. `--smoke-test` guarda JSON y Markdown, usa historial aislado bajo su directorio de salida, recorre las seis vistas y renderiza cada una a los tamaños inicial y mínimo. No ejecuta captura de gaming ni abre ajustes durante esa verificación.

Los PNG representan el área de cliente, sin el marco nativo: 1224 × 781 y 984 × 641 en el escritorio verificado. Se espera un fotograma de WPF antes de cada render para que el cambio de tamaño haya llegado al layout. `status.json` guarda tamaños solicitados y reales. Códigos de salida: 0 éxito, 1 error y 2 argumentos inválidos.

## Verificación

Compilación Release del proyecto App: 0 advertencias y 0 errores. Prueba real con el proveedor de sensores definitivo: 10 muestras, 225 drivers y 2 hallazgos en este equipo (14 de octubre de 2026). La CPU no reportó temperatura; la UI conservó esa ausencia. La prueba generó doce PNG y devolvió código 0; los tamaños reales fueron 1240 × 820 y 1000 × 680.

La revisión visual detectó y corrigió transparencia en el fondo del render, herencia de color en textos, botones primarios con contraste insuficiente y layout de tablas. El cierre durante una captura se verificó mediante cuatro pruebas del coordinador: espera de limpieza, bloqueo de nuevas operaciones al cerrar, reintento tras cancelación y cierre tras fallo. El proyecto de pruebas de Core pasó sus 30 pruebas. La regresión reprodujo primero tres fallos con el comportamiento anterior y pasó con el coordinador corregido; usa operaciones controladas y no inicia PresentMon en el equipo.

No se aplicaron cambios de configuración ni se capturó un juego durante las verificaciones. La ejecución real de PresentMon y la instalación de drivers permanecen acciones voluntarias del usuario.

