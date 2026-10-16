# Silen: diseño de la primera versión

El usuario aprobó avanzar con la propuesta el 13 de octubre de 2026: Windows 11, uso general y gaming, diagnóstico real, drivers, mejoras guiadas e informes. Su indicación «dale nomás» autoriza continuar la implementación con decisiones rutinarias del agente sin nuevas rondas de aprobación.

## Resultado esperado

Una aplicación nativa en español que reúne datos del equipo, identifica oportunidades sustentadas por mediciones y ayuda a realizar cambios mediante las herramientas oficiales de Windows. Debe funcionar sin cuenta y conservar los informes en el equipo. El resultado incluye código compilable y un ejecutable portable para Windows x64.

## Arquitectura

- `Silen.Core`: modelos, reglas de diagnóstico, informes y comparación de sesiones. No depende de Windows ni de la interfaz.
- `Silen.Windows`: inventario WMI, sensores opcionales de LibreHardwareMonitor, consulta de drivers en Windows Update y enlaces oficiales a ajustes.
- `Silen.Gaming`: ejecución de PresentMon opcional e interpretación de CSV de frame times. No modifica juegos ni inyecta código.
- `Silen.App`: WPF con vistas Resumen, Hardware, Drivers, Mejoras, Gaming e Historial. También ofrece un comando de escaneo para comprobar integración.

Los módulos comparten únicamente contratos de Core. La adquisición no bloquea la interfaz. Un fallo de una fuente deja una nota y preserva el resto del diagnóstico. La ausencia de sensores es un dato desconocido, nunca cero.

## Diagnóstico

Inventariar sistema operativo, CPU, GPU, RAM, unidades y espacio libre, adaptadores de red, aplicaciones de inicio, procesos por memoria y plan energético. Tomar aproximadamente diez muestras de CPU, memoria y sensores disponibles. Mostrar temperatura únicamente cuando el proveedor la reporte.

Las reglas son indicadores orientativos, no un diagnóstico causal definitivo:

- Menos del 10% libre en una unidad no vacía: advertir sobre espacio y abrir Almacenamiento.
- Memoria media igual o superior al 85% en al menos tres muestras: advertir y mostrar procesos por memoria.
- CPU media igual o superior al 85% en al menos tres muestras: señalar carga sostenida durante esta observación, sin declarar cuello de botella del juego.
- CPU o GPU con tres lecturas iguales o superiores a 90 °C: aconsejar revisar refrigeración y especificación del fabricante; los límites reales varían por modelo.
- Dispositivo con código de problema distinto de cero: mostrar el código y orientar a Windows Update/fabricante.
- Un driver no firmado merece comprobación de origen; no implica automáticamente malware ni incompatibilidad.
- Plan energético de ahorro detectado: mostrar su coste potencial y abrir ajustes para elegir según batería y uso.
- Aplicaciones de inicio: recomendación informativa para revisar las que no se necesitan; no asumir que todas son perjudiciales.

Los hallazgos incluyen categoría, prioridad, explicación, evidencia y una acción concreta. La edad de un driver no genera por sí sola un hallazgo de desactualización. Las reglas no inventan ganancias de FPS ni una puntuación de salud.

## Drivers

Inventario de dispositivo, proveedor, versión, fecha, firma, clase e incidencias. Consulta separada y voluntaria a Windows Update Agent para actualizaciones de tipo Driver no instaladas ni ocultas. Mostrar estado en curso, número encontrado, ausencia de ofertas o error de consulta. Ausencia de ofertas no significa que no exista otra versión del fabricante.

La primera versión abre Windows Update, actualizaciones opcionales y enlaces oficiales de fabricantes conocidos. La instalación ocurre en la herramienta oficial. No descarga paquetes de repositorios genéricos ni instala todos los drivers automáticamente.

## Mejoras y registro

Cada recomendación abre una acción incluida en una lista explícita: Inicio, Almacenamiento, Energía, Windows Update, drivers opcionales, gráficos, Game Mode, Administrador de tareas o fabricante HTTPS conocido. Registrar que se abrió una guía, no que el ajuste quedó aplicado. Nunca prometer reversión automática de un cambio externo.

## Gaming

Permitir seleccionar un CSV de PresentMon y calcular frames, FPS, media de frame time, percentil 95 y 99, y 1% low definido como 1000 dividido por el frame time medio del 1% más lento. No llamar latencia de entrada al frame time. Documentar la definición de cada métrica.

Permitir seleccionar un ejecutable de consola PresentMon oficial y capturar un proceso durante 15–300 segundos. Validar PID, argumentos, ruta y duración. Ejecutarlo sin shell y conservar el CSV. Si no está instalado o faltan permisos, ofrecer la fuente oficial y la importación CSV. No simular FPS. Comparar dos sesiones mediante métricas equivalentes y advertir que el escenario debe repetirse.

## Historial y privacidad

Guardar snapshots y registro de guías en `%LocalAppData%/Silen`, mediante JSON. Exportar un informe JSON o Markdown elegido por el usuario. Los archivos exportados contienen nombres de procesos y hardware; indicarlo junto al botón de exportación. No recopilar número de serie, nombre de usuario ni hostname. No enviar informes a servicios remotos.

## Interfaz

Ventana redimensionable, tamaño inicial 1240×820 y mínimo 1000×680. Superficies azul tinta `#101A2B`, panel `#17253B`, texto `#EEF4FC`, texto secundario `#A7B8CC`, acento azul `#62B6FF`, aviso ámbar `#F5BF69`. Tipografía Segoe UI Variable/Segoe UI. Barra lateral compacta, indicadores de recursos alineados y filas de recomendaciones con evidencia. Evitar cifras decorativas y resultados simulados.

Estado inicial con invitación a analizar, progreso legible, cancelación y resultados reales. Botones con foco visible y nombres accesibles; tablas desplazables; texto de errores útil. Gaming empieza con instrucciones de captura/importación. Las vistas muestran la fecha de la observación para distinguirla de monitorización continua.

## Verificación

Pruebas de reglas con datos controlados: hardware desconocido, muestras insuficientes, límites de memoria/espacio, drivers antiguos sin errores y temperaturas ausentes. Pruebas de informes en directorios temporales, CSV con distintas cabeceras/cultura, muestras inválidas, percentiles y comparación. Prueba de escaneo real de solo lectura, consulta de drivers si está disponible, compilación Release, publicación portable y render WPF para revisión visual.

No se aplican optimizaciones durante las pruebas en el equipo del usuario.
