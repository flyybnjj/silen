# Optimización ampliada y flujo simple

**Actualización del 15 de octubre de 2026:** el botón principal ahora prepara una
selección independiente de Fortnite, muestra sus efectos y confirma únicamente
lo elegido. Ofrece 18 opciones posibles y 15 controles individuales repartidos
entre Ajustes y Gaming, con un único flujo principal en Resumen. Los resultados
distinguen aplicado, sin cambios, omitido y error. El comportamiento anterior
que se describe debajo queda como registro histórico.
[Flujo actual y catálogo](optimizacion-pc.md).

El usuario pidió un único botón Optimizar PC, confirmación Sí y PC listo al acabar, sin nombres de herramientas, rutas ni lista de ajustes en esa pantalla. El botón prepara un perfil nuevo dentro de la operación compartida y aplica solo los pasos disponibles/predeterminados. PC listo exige resultados completos, sin fallos/cancelación y actualización de respaldos; otros resultados muestran un error. Fortnite abierto requiere cerrarlo. La restauración queda en Mejoras, en un desplegable separado. Los avisos de licencia y procedencia siguen incluidos en el paquete.

Se incorporaron cinco grupos y 16 valores HKCU adicionales, implementados independientemente en `PerformanceTweaks.cs`: apps Store en segundo plano (1), Edge Startup Boost/background (2), contenido promocional (5), personalización diagnóstica (6) y búsqueda web (2). Los destinos y valores están permitidos explícitamente; el journal conserva originales, tipos/ausencia y conflictos. No se ejecuta otro optimizador, se copian servicios completos ni se elimina software. Son preferencias: algunas son de privacidad y pueden no afectar FPS; las de segundo plano pueden limitar notificaciones/sincronización y algunas requieren reabrir apps o sesión.

Fuentes fijadas: [WinUtil, commit 07ccd8e2e755a706f31569808b31f5b77acad6a9](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json), `WPFTweaksDisableBGapps` y subset HKCU de `WPFTweaksTelemetry`; [Optimizer, commit cc88a0e79bde103dd30dcb9fa1f166ac9ad5d924](https://github.com/hellzerg/optimizer/blob/cc88a0e79bde103dd30dcb9fa1f166ac9ad5d924/Optimizer/OptimizeHelper.cs), preferencias de contenido/búsqueda. Se usaron como referencia de configuración, sin copiar código GPL de Optimizer. Edge: [StartupBoostEnabled](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/StartupBoostEnabled) y [BackgroundModeEnabled](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/BackgroundModeEnabled).

La regresión del botón se observó fallar antes de añadirlo y pasa con proveedor de fixture: No no inicia un perfil ni mutaciones, Sí aplica los valores predeterminados disponibles y un resultado parcial nunca dice PC listo. Las pruebas nuevas del backend cubren los cinco grupos, originales/ausencia, tipos y conflicto externo al deshacer. Las pruebas no aplican cambios al PC real.

## Verificación final — 14 de octubre de 2026

Suite completa: 301 pruebas correctas, ninguna fallida y una omitida de red. Compilación y publicación sin errores ni advertencias. El ejecutable publicado pasó 89 comprobaciones de interacción y un diagnóstico real de solo lectura sin SDK en PATH: 10 muestras, 232 drivers, 28 paquetes del catálogo Lenovo y Fortnite detectado con 10 opciones disponibles. No se aplicaron cambios al sistema ni se capturó una partida; no se midieron ganancias de FPS.

Se revisaron las capturas de Gaming a 1240×820 y 1000×680. El botón y su flujo quedan dentro de Silen. La primera revisión detectó un estado inferior obsoleto de detección; se corrigió y la segunda revisión confirmó «Listo para optimizar». Evidencia en `artifacts/simple-optimizer-final/`.

El ZIP final contiene 446 entradas, pesa 68 120 892 bytes y sus ejecutables, ensamblados principales, PresentMon, recurso WinUtil y licencia coinciden con el paquete publicado verificado. SHA-256 de `artifacts/Silen-win-x64.zip`: `0BAB3E1A8549BAFC79F038C829C1D3812D0E8BE5436D8F7332D81E9A2FB16772`.
