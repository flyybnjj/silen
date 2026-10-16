# Herramientas integradas en WPF

Mejoras permite aplicar animaciones de Windows, seleccionar un plan de energía existente, retirar/restaurar entradas Run del usuario actual, previsualizar temporales y eliminar solo los archivos seleccionados. El historial de ajustes incluye el respaldo previo y deshacer con comprobación de conflictos del servicio nativo. La limpieza no tiene deshacer. Las entradas se describen como «Presente en Run» o «Retirada de Run» para distinguirlas de otras fuentes y aprobaciones de inicio.

Gaming ofrece Modo Juego y Capturas Xbox como controles independientes. Explica el alcance de las funciones de grabación Xbox/Game Bar y muestra el plan de energía. El proceso se selecciona de Windows o por PID; la captura resuelve el PresentMon incluido y verificado, sin selector de ejecutable ni descarga externa. Un paquete ausente/manipulado bloquea la captura con instrucciones para recuperar el paquete completo. ETW puede requerir permisos de Windows.

`ToolsViewModel` usa el mismo `RunBusyAsync` y `OperationLifetime` de análisis/captura. Las acciones actualizan el estado real y sus respaldos aun al cancelarse después de una mutación parcial. Un resultado rechazado conserva su motivo, se muestra como error y termina el estado del pie. Una nueva vista previa descarta las selecciones anteriores antes de consultar al servicio, incluso si después se cancela o falla; los IDs invalidados nunca quedan disponibles para limpiar.

La nueva paleta clara y la tipografía de Windows siguen el diseño pedido. Las páginas usan una transición única; los controles tienen estados y movimiento nativo con soporte de la preferencia de movimiento de Windows. Los gráficos y avisos usan tokens compatibles con la paleta. El render de verificación espera `Motion.SettleMilliseconds` antes de capturar.

## Verificación del 14 de octubre de 2026

- Build App Release: cero errores y cero advertencias.
- 56 comprobaciones de interacción verdes en `artifacts/selfcontained-ui-confirm-interactions/interaction-checks.json`, con proveedor nativo de fixture y captura de fixture: no se modificaron ajustes reales ni se inició ETW.
- Regresiones observadas RED y después GREEN: herramientas disponibles sin análisis; cancelación tras cambio parcial; cancelación de una nueva vista previa con selecciones anteriores; ComboBox que mostraba el objeto del plan en lugar de su nombre. La última prueba usa el árbol visual real de WPF y la plantilla incluida.
- Revisión independiente repitió sus reproducciones: la cancelación parcial refleja estado y respaldo actuales; la cancelación de vista previa deja cero archivos y deshabilita limpiar.
- Dos lotes visuales combinados, inicial y confirmación, con las seis vistas a 1240×820 y 1000×680; se incluyeron zonas de inicio, limpieza, deshacer y captura. No se hicieron rondas adicionales de pulido. Se amplió la columna de estado Run tras observar truncado del texto nuevo en la confirmación.
- Smoke real final de fuente: `artifacts/selfcontained-ui-confirm/status.json`, exit 0, 10 muestras, 232 dispositivos, 28 paquetes oficiales por modelo, cero consultas por serie y cero ajustes aplicados. La captura de juego real no se validó en esta tarea.

La suite completa y la publicación del paquete quedan a cargo del agente principal; este trabajo no hizo commit ni publicación.

## Integración de permisos ETW

La verificación del colector publicado devolvió código 6 y acceso denegado al iniciar la sesión ETW con el token normal. La app ofrece «Abrir Silen como administrador» solo después de un error de permisos y cuando su propio proceso aún no está elevado. El clic solicita a Windows abrir únicamente `AppContext.BaseDirectory/Silen.exe` mediante `runas`, sin argumentos y sin cerrar ni transferir la sesión original. No se solicita UAC automáticamente al fallar una captura, analizar ni iniciar la app.

La operación usa el mismo bloqueo y espera de cierre de las herramientas. El error 1223 de Windows muestra la cancelación de UAC junto a la captura y permite reintentar. Un proceso que ya está elevado conserva el error de captura sin ofrecer otra elevación.

La regresión de permiso sin acción se observó RED antes de implementar el flujo; después pasaron 63 comprobaciones de interacción con delegados de fixture (`artifacts/selfcontained-ui-elevation-green/interaction-checks.json`). Cubren ausencia de elevación automática, clic explícito, cancelación y reintento UAC, conservación de la sesión original, propio apphost y bloqueo durante otra operación o cierre. El build App Release terminó con cero errores y advertencias. No se mostró UAC real ni se añadió otra ronda visual.
