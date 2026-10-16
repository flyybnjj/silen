# RAM, red y optimización universal

Solicitud vigente: sustituir Hardware por RAM y red; eliminar Gaming y el perfil
Fortnite; elegir cualquier juego/app y localizar su instalación; centralizar
los ajustes en Optimizar PC sin duplicados. Comprobar DNS con consultas reales.

Confirmación del usuario: retirar Fortnite; la app debe localizar el producto
que el usuario elija. Se conservan los motores históricos para compatibilidad
de respaldos y pruebas, sin exponer ese perfil en la interfaz.

- [x] Diagnóstico RAM en vivo: física disponible, comprometida/límite, procesos
  por memoria y configuración de paginación. Recomendaciones por evidencia del
  equipo, sin vaciar cachés ni prometer liberar RAM. Mejoras de segundo plano
  existentes centralizadas en Optimizar PC; no duplicar acciones en RAM.
- [x] DNS: consultas A reales UDP con comprobación de transacción/pregunta y
  respuesta, timeout/cancelación y TCP en respuestas truncadas. Comparar DNS
  configurados con proveedores públicos, varias consultas por servidor, mediana,
  fallos y progreso. Solo medición explícita, sin cambiar DNS del usuario.
- [x] Catálogo de productos: registro Uninstall por usuario/equipo, manifiestos
  Epic y bibliotecas Steam; resolver ejecutables con búsqueda acotada. No invocar
  Win32_Product, desinstaladores, launchers ni archivos encontrados. Elección
  manual como alternativa para apps portables/no detectadas.
- [x] Optimizar PC: selección de producto/ejecutable y preferencia GPU con
  journal, junto a los ajustes generales ya existentes. No repetir controles de
  Gaming; quitar su pestaña y toda UI específica Fortnite.
- [x] WPF: RAM y red sustituye Hardware; ajustar navegación, búsquedas/anchors y
  pruebas. Preservar Resumen, Drivers, Ajustes, Historial y Bloom.
- [x] Pruebas de motores con datos/controlados, suite .NET10, build/portable,
  comprobaciones WPF y escritorio con respaldo y verificación de archivos.

Fuentes: Microsoft sobre compromiso y paginación (el tamaño depende de la carga,
no de una fórmula universal), APIs GlobalMemoryStatusEx/GetPerformanceInfo;
documentación oficial de Cloudflare/Google/Quad9 para direcciones DNS y Sordum
para el comportamiento de DNS Jumper. No se copian presets TCP ni scripts.

Alcance de optimización por app: preferencia GPU reversible y ajustes de Windows
mostrados como globales. No inventar configuraciones internas para cada juego ni
modificar anti-cheat. Las escrituras usan NativeOptimizationService y journal.
