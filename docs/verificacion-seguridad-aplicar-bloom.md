# Seguridad, Aplicar y Bloom · verificación de integración

16 de octubre de 2026. SDK .NET 10.0.402. Implementación del plan aprobado
`superpowers/plans/2026-10-16-seguridad-aplicar-bloom.md`.

## Cambios entregados

| Grupo | Resultado | Código principal |
| --- | --- | --- |
| A · Bloom | Fachada sin lectura de claves desde el ViewModel; proveedores Gemini/OpenAI/Anthropic, endpoints fijos, límite total de 90 s incluido el cuerpo, salida/contexto y presupuesto acotados. Sumas de consumo saturadas y valores negativos rechazados. | `src/Silen.Core/BloomSession.cs:23`, `src/Silen.Core/BloomConfiguration.cs:38` |
| A · Privacidad | Consentimiento local antes de recopilar el diagnóstico para IA; DTO solo con métricas y recuentos, sin nombres/rutas/notas libres. Cada acción requiere confirmación independiente. | `src/Silen.Core/BloomDiagnosticSummary.cs:4`, `src/Silen.Windows/BloomWindowsTools.cs` |
| B · Privilegios | UI y Bloom sin elevar. Ayudante de acciones tipadas con autenticación del canal/proceso y límites. Desconexión cancela antes del siguiente paso. Journal privilegiado protegido; un journal editable no autoriza undo elevado. | `src/Silen.Windows/SystemActionHost.cs:15`, `src/Silen.Windows/ProtectedOptimizationDirectory.cs:6` |
| C1 · Aplicar | Optimizador principal y manual experto con selecciones independientes y confirmaciones. Resultado por paso; cancelaciones y cambios parciales no se presentan como éxito completo. | `src/Silen.App/ToolsViewModel.Manual.cs:38`, `src/Silen.App/PcOptimizationViewModel.cs` |
| C2 · Reinicio | Después de cambios aplicados: ahora o al cerrar, elección visible y cancelable; confirmación al cerrar, sin forzar programas. Actualizar Silen no reinicia Windows. | `src/Silen.App/RestartViewModel.cs:6`, `src/Silen.Core/RestartPlan.cs` |
| C3 · Temporales | Manifiesto antes del renombrado por handle a recuperación local, sin seguir enlaces. Restaurar sin sobrescribir; purga separada con IDs e identidad revalidados. Solo purgar informa espacio liberado. | `src/Silen.Windows/NativeTemporaryCleanup.Recovery.cs:8`, `src/Silen.App/ToolsViewModel.Recovery.cs:32` |
| D · Actualizaciones | Manifiesto ECDSA P-256 autentica versión, repositorio, asset, tamaño y hash. Falta de firma válida bloquea descarga/instalación. Clave privada del editor cifrada con DPAPI fuera del repo. | `src/Silen.Windows/AppUpdateSignature.cs:6`, `scripts/signing/README.md` |
| D · Dependencias | Locks normales y de publicación separados; publish no modifica el grafo de desarrollo. CI con permisos mínimos y acciones fijadas, auditoría transitiva y búsqueda de patrones de secretos sin imprimirlos. | `Directory.Build.props`, `scripts/build.ps1`, `.github/workflows/security.yml` |
| E · Accesibilidad y movimiento | Claro/Oscuro/Sistema y colores de alto contraste, wordmark con tinta por tema, foco y nombres accesibles, estados de Bloom y pausa del mantenimiento visual oculto/minimizado. Se preservan la geometría y el fondo aprobados. | `src/Silen.App/ThemeManager.cs:8`, `src/Silen.App/RibbonAnimator.cs`, `src/Silen.App/Theme.xaml` |
| Informes | Nuevos diagnósticos guardados y exportaciones sin comandos ni argumentos de inicio. No se altera el snapshot usado para operaciones locales ni se reescriben informes antiguos. | `src/Silen.Core/ReportPrivacy.cs:3` |

## Evidencia y alcance

La suite completa pasó **621 pruebas**: 131 Core, 433 Windows y 57 Gaming; cero
fallos y una consulta OEM de red optativa omitida. Registro:
`artifacts/all-improvements-build.log`. La compilación Release pasó sin errores
ni advertencias. La revisión final independiente confirmó el cierre de los dos
hallazgos del ayudante (cancelación del canal y permiso DeleteChild de ACL), sin
nuevos bloqueantes de severidad alta.

La verificación de WPF ejecuta ventanas y comandos reales con servicios simulados.
Comprueba rechazar/aplicar selecciones, cancelación de reinicio, navegación al manual,
contraste oscuro, consentimiento de Bloom, recuperación y purga por selección.
El paquete completó **198 comprobaciones de interfaz** y conservó sus seis vistas.
El portable se verifica extrayendo el ZIP a una ruta nueva con espacios y sin SDK
en PATH; no se aplica ninguna optimización real.

El diagnóstico por consola y el JSON de smoke también pasan por `ReportPrivacy`;
se reprodujo una exportación con ocho comandos sin omitir y se añadió una
comprobación al verificador portable para rechazar esa regresión sin imprimirlos.

La medición de cinco segundos con ventana activa registró aproximadamente **1,35 %
de CPU**, **193 MiB de conjunto de trabajo** y 89 actualizaciones de la animación.
En tres segundos minimizada registró **0 % de CPU** y **cero actualizaciones**.
Memoria privada: 459 MiB visible y 241 MiB minimizada; memoria administrada cercana
a 7 MiB. Estas muestras no prueban ausencia de fugas ni garantizan el consumo en
otros equipos. No se atribuye a estos datos una medición de GPU.

Los registros finales de portable, escritorio y rendimiento se adjuntan en
`artifacts/all-improvements-portable.log`, `artifacts/all-improvements-desktop.json`
y la ubicación indicada por `artifacts/security-performance-path.txt`.

La comprobación final del ZIP firmó y verificó 33 archivos, 198 comprobaciones y
las seis vistas, sin comandos de inicio en el diagnóstico exportado. Evidencia:
`artifacts/portable verification 580f4ab226174543a7c4fdfd26819830`.
Los 13 archivos de bloqueo permanecieron idénticos tras publicar.

Entrega local: `C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`, acceso
directo `Silen.lnk` actualizado y 33 archivos copiados y comparados por SHA-256.
La versión anterior permanece en `Silen - preversion anterior e8a1ea67`.

- EXE SHA-256: `D23A45000727C85A0000C7C8798C043CC05D8F5908A43818CB65A05F9785F111`.
- ZIP SHA-256: `57144B80BF05760CF18284FC27AE84C50B534D41D5AB3589F64CC5800DBC9CBB`.
- Manifiesto: `artifacts/Silen-release.json`, ECDSA P-256/SHA-256, tag `v1.1.0`.

## Límites explícitos

- Las pruebas de proveedores usan HTTP simulado: no se leyó la clave real ni se
  consumió cuota. Disponibilidad y cargos requieren una cuenta válida del proveedor.
- El ayudante se prueba con canales locales y plataformas simuladas; no se pidió
  UAC ni se modificaron ajustes reales. Tampoco se reinició Windows.
- La firma del manifiesto no es un certificado Authenticode público; Windows
  puede seguir mostrando advertencias de editor desconocido. No se publicó una
  release externa ni se ejecutó CI remoto.
- Los respaldos del sistema heredados editables no se aceptan como autorización
  elevada. Se conservan datos previos; los nuevos respaldos privilegiados se
  consultan con autorización desde Ajustes.
- La recuperación está dentro de temporales; otro limpiador o Windows puede
  retirarla. No es un respaldo permanente. La purga no tiene deshacer.
- Alto contraste se verificó con los recursos del sistema y fixtures, sin cambiar
  las preferencias de Windows. No equivale a certificación manual con Narrador.
- La medición de reposo corresponde a muestras cortas de este equipo, con CPU
  normalizada a todos los núcleos. No mide GPU ni el costo completo del compositor.
- El escáner de secretos revisa archivos versionados y nuevos no ignorados por
  patrones; no constituye una auditoría completa del historial de Git.

La revisión automática rechazó borrar la clave de prueba cifrada
`artifacts/signing-fixture/test-key.dpapi` con «blocked by policy», sin otro motivo.
Se conservó intacta, ignorada y fuera del portable.
