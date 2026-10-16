# Optimización del PC

Optimizar PC tiene un único acceso principal en Resumen, sin analizar primero ni
instalar un juego concreto. Preparar la selección solo consulta estados. Aplicar selección
muestra una confirmación con los títulos elegidos y ejecuta únicamente esos pasos.
Un fallo no se presenta como una optimización completa; cada resultado informa
si se aplicó, ya estaba así, se omitió por disponibilidad o no pudo completarse.

## Catálogo

Hay 20 opciones generales posibles, más GPU, temporizador y dos controles de entrada
al elegir un ejecutable, y entradas individuales de inicio del usuario (hasta 100 por revisión),
con disponibilidad comprobada para cada equipo. Los controles generales de Gaming
se concentran aquí; la pestaña Gaming se retiró. En las
preferencias agrupadas y servicios, Restaurar recupera un respaldo de Silen;
si no existe, no se inventa un supuesto valor predeterminado de Windows.

| Opción | Alcance y efecto |
| --- | --- |
| Modo Juego | Preferencias del usuario para Modo Juego de Windows. |
| Capturas Xbox | Permitir o desactivar la grabación Xbox/Game Bar; conserva Xbox y OBS. |
| Animaciones de ventanas | Preferencia de animación del área cliente mediante Windows. |
| Funciones de grabación Game Bar | Audio/cursor, apertura con mando y panel inicial. |
| Composición del escritorio | Transparencias y Aero Peek; puede requerir volver a iniciar sesión. |
| Apps en segundo plano | Preferencia de apps Store; puede limitar notificaciones o sincronización y depende de Windows. |
| Edge al cerrarlo | Desactiva Startup Boost y ejecución de apps/extensiones en segundo plano; reabrir Edge. |
| Chrome al cerrarlo | Política oficial BackgroundModeEnabled=0; limita apps/extensiones en segundo plano. Opcional, con detección de instalación por App Paths y respeto de la política del equipo. No cierra pestañas ni procesos actuales. Algunas tareas iniciadas pueden continuar. |
| Entradas de inicio del usuario | Elección individual de entradas HKCU Run. Retira solo la entrada elegida para próximas sesiones; la app puede tener otros mecanismos de inicio. No cierra procesos ni desinstala apps. Cada comando y tipo original quedan respaldados. |
| Contenido promocional | Preferencias de recomendaciones e instalaciones silenciosas; no desinstala apps. |
| Personalización diagnóstica | Publicidad y personalización de escritura; no desactiva toda la telemetría. |
| Búsqueda web | Preferencias de sugerencias web; su efecto depende de Windows y sus políticas. |
| Intercambio de actualizaciones | DODownloadMode=0, descargas HTTP sin pares. No detiene Windows Update o Store. Requiere administrador. |
| Tráfico de actualizaciones al jugar | DOPercentageMaxBackgroundBandwidth=10 limita descargas en segundo plano de Delivery Optimization al 10 % del ancho de banda estimado. No limita otros launchers ni garantiza menor ping. Requiere administrador. |
| Power Throttling | Política de equipo que desactiva la reducción de CPU para procesos elegibles. Requiere administrador y corriente para aplicarla. Persiste al desconectar hasta restaurarla; puede aumentar consumo y temperatura. |
| DiagTrack | Detiene y deshabilita experiencias conectadas y diagnósticos del servicio. Opcional; no implica eliminar toda la telemetría. |
| MapsBroker | Detiene y deshabilita mapas descargados; afecta apps que dependen de mapas sin conexión. |
| Fax | Detiene y deshabilita el servicio cuando existe; afecta envío y recepción de fax. |
| Modo Mejor rendimiento | Usa el modo moderno sobre un plan Equilibrado compatible y con corriente. |
| Plan Alto rendimiento existente | Alternativa cuando el modo moderno no está disponible; no crea planes adicionales. |
| Efectos visuales del escritorio | Reduce transiciones de menús, listas y ayudas, conservando cada preferencia. |

La selección recomendada incluye Modo Juego, segundo plano de Edge y una opción
de energía compatible, solo si están disponibles y aún necesitan un cambio.
No desactiva servicios ni capturas por defecto; Chrome e inicio también requieren
elección manual. Actualizar disponibilidad conserva la selección del usuario.
Recomendados permite volver a la selección sugerida;
Quitar selección desmarca todas las opciones.

Los ajustes que necesitan permisos lo indican antes de aplicar. La interfaz y Bloom
no se elevan: un ayudante autentica la sesión y ejecuta solo la acción tipada autorizada.
Cancelar el permiso no aplica ese paso y permite reintentar. El protocolo exige
la misma cuenta de Windows; no acepta otro usuario para alterar preferencias.

El optimizador manual de Ajustes, visible en modo experto, prepara un lote independiente.
Sus controles añaden o retiran cambios pendientes; **Aplicar selección manual** enumera
los efectos y ejecuta solo lo confirmado. El optimizador principal conserva su propia selección.
Tras aplicar un lote se ofrece reiniciar ahora o al cerrar Silen, con cancelación y
confirmación final. No fuerza el cierre de otras aplicaciones. Cada opción indica
si actúa inmediatamente, al reabrir una aplicación, al iniciar sesión o al reiniciar.

## Respaldo y recuperación

Cada escritura guarda primero el original en OptimizationJournal. El respaldo
conserva presencia o ausencia, tipo y valor del registro. Los nuevos destinos
de equipo llevan un ámbito HKLM explícito; los respaldos antiguos de usuario
siguen interpretándose como HKCU. Los servicios guardan tipo de inicio y si
estaban ejecutándose. Su cuenta, ejecutable, dependencias, inicio demorado y
opciones de recuperación no se modifican.

Restaurar revalida el estado y no sobrescribe un cambio externo incompatible.
Ante un fallo parcial o cancelación, los respaldos siguen en Ajustes. Un servicio
deshabilitado que aún está en ejecución no se detiene: volver a iniciarlo sin
cambiar su configuración original no sería posible. Los servicios ausentes,
en transición o sin permisos no se ofrecen para aplicar.

La limpieza irreversible queda separada, con un botón que muestra el tamaño
calculado. Al pulsarlo vuelve a consultar los temporales elegibles de al menos
24 horas y revalida cada archivo antes de eliminarlo. Omite archivos cambiados,
ocupados y enlaces, e informa el espacio liberado. El cálculo está acotado:
si queda contenido pendiente, lo indica y recalcula el siguiente lote.
No se vacían cachés de shaders automáticamente: su regeneración puede causar
tirones. No se aplican claves TCP de forma indiscriminada ni se promete menor ping.
Tampoco se modifica anti-cheat, Defender ni servicios críticos.

## Aplicación elegida y Windows Update

Optimizar PC reúne Modo Juego, capturas, tráfico de actualizaciones, planes de energía
y preferencia de GPU por ejecutable local elegido. Esta última se guarda en
HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences, preserva los otros campos
y permite restauración aunque después se desinstale el juego. No cambia una
prioridad global de GPU ni ejecuta el archivo seleccionado.

La selección de producto consulta el registro de aplicaciones y manifiestos Steam
y Epic; no ejecuta los archivos encontrados. El usuario elige su ejecutable y vuelve
a preparar la selección. Cambiar el objetivo invalida la revisión anterior. La
preferencia GPU y el perfil de temporizador corresponden al ejecutable revisado;
los ajustes de entrada y los demás pasos son globales de Windows.
Si la preferencia GPU ya es de alto rendimiento, se indica como presente y no se
reescribe el valor ni sus otros campos. Una lectura sin estado concluyente se
presenta como tal, sin atribuir una GPU efectiva a Windows.
Se retiró el perfil de Fortnite de la interfaz, conservando sus respaldos históricos.

El temporizador consulta el mínimo admitido, detecta la ruta completa del proceso
cada dos segundos y mantiene una solicitud propia mientras corre el producto y
Silen permanece abierto. Al terminar, desactivar el perfil o salir de Silen se
libera únicamente esa solicitud. La opción es experimental y queda sin marcar
inicialmente; Windows 11 no garantiza efecto sobre otro proceso. No se cambian
HPET, BCD, servicios ni controladores. Los perfiles guardados se desactivan también
desde Ajustes, incluso después de desinstalar el producto.

Las opciones de entrada tampoco se preseleccionan. FilterKeys usa aceptación
0 ms, demora/repetición 130/10 ms y rebote 0 ms; desactiva el atajo y los sonidos
por pulsación. Ratón sin aceleración pone sus umbrales y aceleración en cero.
No cambia sensibilidad, DPI ni frecuencia USB. Antes de cada escritura SPI se
guardan todos los campos anteriores en OptimizationJournal. Undo verifica el
estado actual y recupera el original sin sobrescribir cambios externos.
Son preferencias globales que permanecen hasta restaurar; no se reponen
automáticamente al iniciar el juego. [Detalle y fuentes](temporizador-entrada-experto.md).

Ajustes se abre pulsando el logo **silen**. Su modo experto muestra el botón
**Optimizador manual**, que revela los controles individuales de Windows,
energía e inicio. El modo normal conserva limpieza, restauración y control de
perfiles. Guardar la preferencia de modo no modifica la configuración de Windows.

Las entradas de inicio no se seleccionan por defecto. Se excluyen nombres y rutas
reconocibles de seguridad, anti-cheat y componentes de Windows; esto no clasifica
todas las aplicaciones posibles. El usuario elige únicamente lo que no necesita.
Si el comando cambia desde la revisión o se pierden permisos, se conserva la
entrada y se informa el paso pendiente. La lista no es un inventario de todo el
inicio de Windows: quedan fuera tareas programadas, servicios, carpetas de inicio
y apps Store. La presencia en HKCU Run tampoco demuestra que Windows la ejecute:
puede estar deshabilitada en Configuración/Administrador de tareas. La interfaz
indica actividad sin verificar; no se atribuye un consumo o beneficio por esa
presencia. Ajustes mantiene la habilitación y restauración de respaldos.

Chrome se detecta por registros App Paths de usuario o equipo y un ejecutable
local existente. Una instalación portable sin registro puede no aparecer como
disponible. Si una política de equipo controla el modo de segundo plano, no se
escribe una política de usuario para intentar anularla. Restaurar recupera la
preferencia anterior exacta; mientras la política está aplicada, Chrome puede
mostrar que esta opción está administrada. No es un ahorro de RAM garantizado.

Antes de consultar drivers, se leen las fechas locales UX, las políticas de pausa,
los indicadores efectivos de Windows y el estado/configuración de wuauserv.
Un servicio manual detenido se informa como tal y se ofrece iniciarlo explícitamente.
Reanudar retira las fechas locales y activa el servicio cuando corresponde;
si estaba deshabilitado, lo deja en inicio manual. Cada cambio guarda su original
antes de escribirse. No se eliminan políticas GPO/MDM ni se sobrescriben indicadores
efectivos de Windows. Si Windows mantiene una pausa activa, se informa que no se
confirmó la reanudación y los respaldos siguen disponibles para restaurar.

## Referencias

- [Calidad de servicio y Power Throttling](https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service).
- [Delivery Optimization](https://learn.microsoft.com/en-us/windows/deployment/do/delivery-optimization-configure) y [política DODownloadMode](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-deliveryoptimization).
- [Funciones y consecuencias de los servicios](https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services). Esta guía corresponde a Windows IoT Enterprise: se usa para documentar efectos, no como recomendación universal para equipos domésticos.
- [Configuración de servicios con SCM](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-changeserviceconfigw).
- [MMCSS](https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service): valores SystemResponsiveness menores de 10 se limitan a 20; no se añade el supuesto ajuste a cero como mejora de latencia.
