# Perfil Fortnite: fuentes y alcance

Investigación verificada el 14 de octubre de 2026. El perfil combina una selección oficial de WinUtil con ajustes propios de Silen para Fortnite. Los recursos se incluyen en la aplicación; no se ejecuta PowerShell de terceros ni se descarga código durante la optimización.

## Selección de WinUtil

Repositorio oficial [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil), fijado al commit `07ccd8e2e755a706f31569808b31f5b77acad6a9` del 7 de octubre de 2026. El archivo [config/tweaks.json fijado](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json) contiene `WPFToggleGameMode`:

| Ruta | Nombre | Tipo | Valor activado |
| --- | --- | --- | --- |
| `HKCU\Software\Microsoft\GameBar` | `AllowAutoGameMode` | DWORD | 1 |
| `HKCU\Software\Microsoft\GameBar` | `AutoGameModeEnabled` | DWORD | 1 |

`assets/WinUtil/fortnite-winutil.json` conserva el objeto completo bajo su nombre original, incluyendo descripción, categoría, enlaces y los dos objetos de registro. Los valores `Value`, `OriginalValue` y `DefaultState` permanecen como cadenas, igual que en el original. SHA256 del recurso: `354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B`.

El backend valida los destinos admitidos y conserva el estado real previo, incluidos tipo y ausencia de valores; no usa `OriginalValue` como sustituto de un respaldo. `assets/WinUtil/provenance.json` registra origen, commit, extracción y hashes. La [licencia MIT fijada](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/LICENSE) se conserva íntegra en `licenses/WinUtil-MIT.txt`, con copyright de CT Tech Group LLC. SHA256 de la licencia: `469B54DADFA5C0D0002291E452FCAA32165356D3459E1DEE040404DB5439FB1C`.

La configuración actual no contiene `WPFTweaksDVR`. El ajuste de capturas Xbox de Silen debe identificarse como propio. `WPFTweaksDisplay` modifica múltiples preferencias de interfaz/teclado y un valor binario; no equivale a la llamada SPI de animaciones que ya utiliza Silen. El perfil no incorpora esos cambios amplios. `WPFAddUltPerf` se clasifica para equipos que no sean laptops y su [implementación](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/public/Invoke-WPFUltimatePerformance.ps1) duplica planes y restaura todos los planes predeterminados al deshacer. Silen selecciona únicamente un plan existente y conserva el anterior, condicionado a alimentación AC.

## Fortnite y preferencias del usuario

La [guía competitiva oficial de Epic, publicada el 30 de abril de 2026](https://store.epicgames.com/news/fortnite-on-pc-best-settings-for-competitive-play-in-2026?lang=en-US), recomienda resolución nativa, VSync desactivado, calidad baja, distancia Near, texturas y meshes Low, y mostrar FPS. El modo Performance actual utiliza DirectX 12. Los ajustes tienen un coste de fidelidad visual y sus resultados dependen del hardware; no justifican anunciar una cantidad de FPS ganados sin medirla.

Epic documenta la ruta `%LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini` e indica quitar el atributo de solo lectura para guardar preferencias. Silen conserva las opciones ajenas al perfil y respalda el archivo; no lo congela como solo lectura. [Soporte oficial de ajustes que no se guardan](https://www.epicgames.com/help/c-34254770/c-0/a22311370?lang=en-US).

Las opciones de calidad se trasladan a claves existentes conocidas del INI. Esta traducción es una implementación de Silen, no una API pública específica de Fortnite. La [referencia de escalabilidad de Unreal](https://dev.epicgames.com/documentation/en-us/unreal-engine/scalability-reference-for-unreal-engine) documenta grupos de calidad y valores bajos como 0; no autoriza editar archivos internos del juego, `Engine.ini`, paquetes o anticheat.

El perfil no inventa una clave para cambiar el renderizador. Epic documenta `-d3d12 -FeatureLevelEs31` para Performance en los argumentos del Launcher y requiere reiniciar el juego. Estos pasos pueden mostrarse dentro de Silen como guía. [Soporte oficial de modo de renderizado](https://www.epicgames.com/help/c-34254770/a22616652).

Para equipos híbridos, [Epic recomienda seleccionar la GPU de alto rendimiento](https://www.epicgames.com/help/c-34254770/c-38015632/a14140539). [Microsoft documenta esa preferencia por aplicación](https://support.microsoft.com/en-au/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11). No se encontró una API pública que documente escribir `UserGpuPreferences`: una implementación por registro debe conservar el valor original, describirse como preferencia y comprobar su efecto tras reiniciar Fortnite, sin garantizar qué GPU usará el cliente.

## Detección de la instalación

No se encontró una especificación pública oficial de Epic para el esquema `.item`. La fuente primaria de interoperabilidad es [Legendary, commit `42f6bdeadde3a9526dc8eb713763476999ac217b`, lector EGL](https://github.com/derrod/legendary/blob/42f6bdeadde3a9526dc8eb713763476999ac217b/legendary/lfs/egl.py): lee JSON en `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, indexado por `AppName`. Su [modelo EGL](https://github.com/derrod/legendary/blob/42f6bdeadde3a9526dc8eb713763476999ac217b/legendary/models/egl.py) incluye `InstallLocation`, `LaunchExecutable`, `bIsIncompleteInstall` y `bNeedsValidation`.

Detectar Fortnite mediante esos campos es una inferencia basada en esa implementación, no una API oficial Epic. La lectura debe tener límites de tamaño/cantidad, rechazar JSON inválido y rutas no admitidas, comprobar el ejecutable dentro de la raíz encontrada y ofrecer selección manual. No debe modificar manifests, mover el juego ni cambiar archivos del Launcher.

## Límites del perfil

El botón aplica cambios enumerados y respaldados, informa lo aplicado, omitido o fallido, y permite deshacer. No ejecuta presets generales remotos, elimina servicios, altera anticheat, desactiva seguridad/actualizaciones ni hace overclock o cambios de temporizadores/BCD. La captura incluida permite comparar FPS y tiempos de cuadro antes y después bajo la misma carga; hasta entonces solo se puede afirmar que se aplicaron preferencias de rendimiento.
