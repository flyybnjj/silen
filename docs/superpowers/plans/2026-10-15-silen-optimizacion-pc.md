# Optimización del PC y ampliación de ajustes

**Objetivo:** convertir Optimizar PC en un flujo independiente de Fortnite, con
selección revisable, resultados comprobados y restauración exacta; ampliar las
opciones funcionales sin cambiar la identidad visual.

**Arquitectura:** conservar INativeOptimizationService/OptimizationJournal y
añadir un orquestador de PC con vista previa, aplicación secuencial y progreso.
El catálogo existente se expone completo en Ajustes. Los servicios se controlan
mediante SCM y solo se permiten DiagTrack, MapsBroker y Fax. Las políticas de
equipo usan un ámbito HKLM explícito, conservando el formato HKCU anterior.

**Tecnología:** .NET 10, C#, WPF/MVVM. Sin scripts externos ni dependencias nuevas.

## Requisitos y límites

- No modificar la configuración del equipo de desarrollo para probar ajustes.
- Journal duradero antes de cada escritura; Undo conserva cambios externos.
- Servicios y políticas avanzadas quedan sin seleccionar inicialmente.
- Energía con corriente comprobada de nuevo antes de escribir; sin crear planes.
- No modificar anti-cheat, Defender, Windows Update ni servicios críticos.
- No vaciar shaders automáticamente: obliga a recompilar y puede causar tirones.
- No prometer menor ping ni FPS. SystemResponsiveness=0 no se añade: Microsoft
  documenta que valores menores de 10 se limitan a 20. No aplicar claves TCP en
  el contenedor Interfaces sin identificar una interfaz y una necesidad real.
- La limpieza irreversible sigue separada y requiere una selección explícita.
- Mantener compatibilidad de los journals anteriores y la preversión del escritorio.

## Tareas y pruebas

- [x] Exponer preferencias existentes, capacidad de aplicar/restaurar y categorías.
  Pruebas: inspección sin escrituras, estado original con tipo exacto, sin respaldo
  no se inventa una restauración, permisos denegados sin mutaciones.
- [x] Añadir políticas opcionales PowerThrottlingOff y DODownloadMode=0 en HKLM;
  servicios opcionales con estado de inicio y ejecución respaldados. Pruebas:
  ámbito correcto, servicio ausente/restringido, fallo parcial y recuperación,
  conflicto externo, intento de modificar un servicio fuera de catálogo rechazado.
- [x] Orquestador PC independiente de Fortnite. Vista previa de solo lectura,
  selección recomendada conservadora, aplicar solo selección disponible, revalidar
  al aplicar y resultado por paso; no borrar selecciones del usuario. Pruebas de
  batería, selección inválida, cancelación, fallo parcial y repetición idempotente.
- [x] Conectar un único Optimizar PC, selector y resultados en Resumen, conservar
  perfil Fortnite en Gaming. Añadir pruebas de interacción nativas para comprobar
  que preparar/declinar no aplica, selección y progreso reales y ausencia de juego.
- [x] Suite completa, build sin advertencias, portable extraído, inspección visual
  del flujo funcional y actualización verificada de la preversión del escritorio.

## Fuentes técnicas

- https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service
- https://learn.microsoft.com/en-us/windows/deployment/do/delivery-optimization-configure
- https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-deliveryoptimization
- https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services
  (referencia de función/efectos, no recomendación universal para Windows doméstico).
- https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-changeserviceconfigw
- https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_delayed_auto_start_info

## Evidencia inicial

`artifacts/silen-expanded-options-red.log`: tres pruebas fallan por preferencias
ocultas, imposibilidad de aplicar/restaurar desde un toggle y política de red ausente.
Las otras dos ya cubren rechazo seguro de restauración inventada/permisos.

## Entrega de esta ampliación

460 pruebas correctas y una comprobación OEM de red omitida, build sin advertencias
ni errores y 144 comprobaciones del portable extraído. Revisión independiente:
se corrigió duplicación del flujo principal y se verificó elevación separada de
captura, sin autoaplicar. Evidencia `artifacts/silen-pc-final-{tests,build,portable}.log`.
Preversión del escritorio actualizada y 30 archivos cotejados por SHA256;
la anterior se conserva en `Silen - preversion anterior`.
