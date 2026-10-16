# Mejoras del motor basadas en guías y proyectos conocidos

Consulta: 15 de octubre de 2026. Solicitud: mejorar el optimizador principal,
usando guías y repositorios populares como referencias, sin cambios cosméticos.

## Repositorios examinados

Estrellas verificadas con la API pública de GitHub; los datos exactos se conservan
localmente en `artifacts/optimizer-research-repositories.json`.

| Proyecto | Estrellas | Estado | Uso |
| --- | ---: | --- | --- |
| [WinUtil](https://github.com/ChrisTitusTech/winutil) | 64.283 | Activo, MIT | Organización de ajustes individuales y reversión. Se revisó su catálogo de tweaks. |
| [Win11Debloat](https://github.com/Raphire/Win11Debloat) | 59.339 | Activo, MIT | Diferenciar reducción de actividad y privacidad de desinstalación de componentes. |
| [System Informer](https://github.com/winsiderss/systeminformer) | 16.248 | Activo, MIT | Interpretación de recursos y procesos; una cifra de RAM libre no equivale a rendimiento. |
| [Optimizer](https://github.com/hellzerg/optimizer) | 18.243 | Archivado desde enero de 2026, GPL-3.0 | Comparación histórica; no se usa como dependencia ni guía vigente de mantenimiento. |

Se leen documentación y archivos públicos de texto. No se descargan ejecutables,
no se ejecutan scripts de estos proyectos y no se copia su código al motor.
Las estrellas sirven para seleccionar referencias; cada ajuste se contrasta con
su documentación y con el comportamiento real del código de Silen.

## Qué implementar primero

1. **Inicio de sesión por app, dentro de Optimizar PC.** Microsoft identifica las
   apps de inicio como causa de carga adicional y documenta HKCU Run. Silen ya
   respalda estas entradas, pero solo las expone en Ajustes. Integrar las entradas
   registradas como opciones individuales, sin ninguna preseleccionada. La
   presencia en Run no verifica actividad: Windows puede tener la app deshabilitada
   mediante Configuración/Task Manager. No atribuirle consumo por su presencia. No cerrar
   procesos actuales ni borrar instalaciones. Volver a comprobar el comando
   exacto al aplicar; si cambió, omitirlo y pedir una nueva revisión. Excluir
   entradas reconocibles de seguridad, anti-cheat y componentes de Windows.
   El beneficio, cuando existe, corresponde al próximo inicio de sesión.
2. **Chrome en segundo plano.** La política oficial `BackgroundModeEnabled=0`
   impide el modo de segundo plano cuando se cierra la última ventana. No es un
   limpiador de RAM ni cambia pestañas abiertas. Presentar solo como opción
   manual, con disponibilidad según registro App Paths y permisos. Puede afectar
   apps web/extensiones; una política administrativa puede prevalecer. Preservar
   el valor original exacto, incluso su ausencia o tipo.
3. **Estado real de GPU por app.** Windows ofrece preferencia de GPU por
   aplicación; Silen ya la aplica. Detectar si el ejecutable elegido ya tiene la
   preferencia de alto rendimiento y distinguir estado conocido de lectura no
   disponible. No repetir un cambio existente ni sobrescribir otros campos.

Se compararon tres caminos: importar scripts completos, acumular presets globales
o ampliar las operaciones nativas existentes. Se eligió el último: reutiliza
OptimizationJournal, permite pruebas sin tocar el PC y conserva acciones visibles
por separado antes de confirmar.

## Otras mejoras evaluadas

- Las optimizaciones de juegos en ventana están documentadas para juegos DX10/11
  compatibles. Su automatización exige comprobar versión, estado efectivo y
  excepciones por app; no se añade una escritura de registro supuesta a esta
  entrega. La preferencia GPU existente sí está integrada.
- Servicios opcionales, energía, Game Mode, capturas y Delivery Optimization ya
  están en el motor. No añadir duplicados ni atribuirles ganancias universales.
- No desactivar Defender, HVCI, Windows Update, SysMain o Search como receta general;
  no forzar HPET, prioridades de tiempo real ni ajustes TCP para todo el tráfico.
- No borrar cachés de shaders como optimización recurrente: su reconstrucción
  puede implicar trabajo adicional. La limpieza segura de temporales se mantiene
  separada de mejoras de rendimiento y conserva su confirmación.

## Documentación primaria

- [Microsoft: mejorar el rendimiento de Windows](https://support.microsoft.com/en-gb/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows).
- [Microsoft: configurar aplicaciones de inicio](https://support.microsoft.com/es-es/windows/experience/startup-boot/configure-startup-applications-in-windows).
- [Microsoft: Run y RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys).
- [Microsoft: App Paths](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration).
- [Google: BackgroundModeEnabled](https://chromeenterprise.google/policies/background-mode-enabled/).
- [Microsoft: optimizaciones para juegos en ventana y GPU por app](https://support.microsoft.com/es-es/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11).
- [Microsoft: QoS y consumo](https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service).

Los resultados de una optimización se informan como ajustes aplicados, omitidos
o ya presentes. Sin medición comparable de una carga concreta no se promete
un aumento de FPS, reducción de RAM o menor ping.
