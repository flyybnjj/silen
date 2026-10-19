# AGENTS.md · Silen

Guía operativa y estándar de desarrollo para agentes de IA (OpenAI Codex, Claude, Cursor, Antigravity) que trabajen en este repositorio.

---

## 1. Identidad y Misión del Proyecto

**Silen** es una aplicación nativa de escritorio para **Windows 11 x64**, desarrollada en **C# y WPF sobre .NET 10**.
Su propósito es diagnosticar hardware, gestionar drivers oficiales y **aplicar optimizaciones reales de rendimiento para Windows y Gaming (Fortnite, latencia de entrada y respuesta del sistema)**, con respaldo y restauración garantizados.

* **Stack:** C# 13 / .NET 10 (WPF), arquitectura portable autocontenida (`win-x64`).
* **Estilo visual:** Minimalista, sobrio y funcional (tema lino/tinta/rojo ladrillo).
* **Idioma:** Interfaz y mensajes siempre en **español neutro / chileno**.

---

## 2. Reglas de Oro del Agente (Strict Constraints)

> [!CRITICAL]
> **1. Prioridad Funcional Absoluta:**
> Cuando el usuario solicite *"agregar más opciones"*, *"revisar"*, *"mejorar"* o mencione que la app *"es básica"*, **NO propongas ni hagas cambios cosméticos** (fuentes, logos, animaciones de fondo, colores ni márgenes). 
> **Tu foco inmediato debe ser implementar ajustes de optimización reales del sistema** en el motor de `Silen.Windows` y exponerlos en `Silen.App`.

* **2. Silen es un Optimizador General (No solo Fortnite):**
  Fortnite es solo un perfil opcional. Silen debe enfocarse en rendimiento global de Windows, latencia de red, servicios, energía y cualquier juego. La pestaña Gaming debe ofrecer optimizaciones universales de PC y dejar Fortnite como una sección secundaria si está instalado.
* **3. Diagnóstico Real de Windows Update:**
  No asumir que Windows Update funciona solo llamando a COM. Comprobar si las actualizaciones están pausadas (`HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings`) o si el servicio `wuauserv` está deshabilitado, informar el estado real al usuario y ofrecer reanudarlas o repararlas.
* **4. Respaldo y Reversibilidad Obligatoria:** 
  Cualquier modificación al registro de Windows, servicios o configuración de juegos DEBE registrar su estado previo en `OptimizationJournal` para permitir restaurarla con `UndoAsync`.
* **5. Simplicidad de Interfaz (Cero Redundancias):**
  Evitar botones duplicados (no repetir el botón de optimización en múltiples pestañas), eliminar exportaciones técnicas innecesarias para el usuario común (JSON/MD en pantalla principal) y evitar botones que solo cambian de tab.

---

## 3. Arquitectura del Código

El repositorio sigue una arquitectura limpia modular:

```text
flyyoptimizer/
├── src/
│   ├── Silen.Core/           # Modelos base, logging, contratos transversales y rutas.
│   ├── Silen.Windows/        # MOTOR DE OPTIMIZACIÓN DEL SISTEMA
│   │   ├── PerformanceTweaks.cs          # Catálogo central de tweaks de registro agrupados.
│   │   ├── NativeOptimizationService.cs  # Inspección, aplicación y rollback con journal.
│   │   ├── OptimizationContracts.cs      # Interfaces e inmutables (ToggleSetting, Overview, etc.).
│   │   ├── NativeTemporaryCleanup.cs     # Limpiador de archivos temporales.
│   │   ├── WindowsCollector.cs           # Diagnóstico de CPU, GPU, RAM, red y discos.
│   │   └── FortniteSettingsEditor.cs     # Ajustes competitivos seguros de Fortnite.
│   ├── Silen.Gaming/         # Métricas de PresentMon y análisis de sesiones de juego.
│   └── Silen.App/            # INTERFAZ DE USUARIO (WPF / MVVM)
│       ├── MainWindow.xaml               # Vistas principales (Resumen, Hardware, Drivers, Ajustes, Gaming, Historial).
│       ├── ToolsViewModel.cs             # ViewModel de la pestaña "Ajustes" (Toggles, Energía, Limpieza, Undo).
│       ├── FortniteViewModel.cs          # ViewModel de la pestaña "Gaming".
│       └── MainViewModel.cs              # Orquestador general del ciclo de vida.
├── scripts/
│   ├── build.ps1             # Compilación, ejecución de tests y empaquetado portable.
│   └── run.ps1               # Ejecución directa en desarrollo.
└── tests/                    # Pruebas unitarias de regresión y cobertura.
```

---

## 4. Guía para Agregar Nuevas Opciones de Optimización

Para expandir las opciones de Silen y atender la demanda de los usuarios, sigue este flujo estándar:

### Paso A: Registrar el nuevo grupo en `PerformanceTweaks.cs`
Todo ajuste de registro se define como un `Group` con sus `Value` asociados:

```csharp
// Ubicación: src/Silen.Windows/PerformanceTweaks.cs
new("network-latency", "Optimizar latencia de red y paquetes", 
    "Desactiva el algoritmo de Nagle (TcpAckFrequency/TCPNoDelay) y elimina la limitación de red multimedia para reducir el ping.",
    [
        new(@"System\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TcpAckFrequency", 1),
        new(@"System\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TCPNoDelay", 1),
        new(@"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1),
        new(@"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0)
    ]),
```

### Paso B: Exponer en `NativeOptimizationService.cs` y `ToolsViewModel.cs`
1. En `NativeOptimizationService.InspectAsync()`, agregar el toggle a la lista `toggles` leyendo el estado actual con `InspectToggle(...)`.
2. En `ToolsViewModel.RefreshStateAsync()`, añadir el nuevo toggle a `ImprovementToggles` o `GamingToggles` según corresponda.
3. `MainWindow.xaml` automáticamente renderizará el nuevo control gracias a los `DataTemplate` dinámicos (`OptimizationToggleTemplate`).

### Paso C: Módulos prioritarios solicitados por los usuarios:
* **Red y Latencia (Network & Ping):** `TcpAckFrequency`, `TCPNoDelay`, `NetworkThrottlingIndex` = `ffffffff`, `SystemResponsiveness` = `0`.
* **Servicios Innecesarios (Debloat):** Servicios seguros de telemetría y diagnóstico (`DiagTrack`, `MapsBroker`, `Fax`).
* **Caché y Limpieza Profunda:** Limpieza de caché de Shaders de DirectX/NVIDIA y temporales residuales.
* **Energía y Throttling:** Plan *Ultimate Performance* (`e9a42b02-d5df-448d-aa00-03f14749eb61`) y desactivación de *Power Throttling*.

---

## 5. Comandos de Compilación, Pruebas y Verificación

El entorno utiliza el SDK bundled de .NET 10 provisto por Codex o el sistema:

* **Ejecutar Pruebas Unitarias:**
  ```powershell
  & "$env:USERPROFILE\.codex\runtimes\dotnet-10-sdk\dotnet.exe" test Silen.sln -c Release
  ```
* **Compilar y Generar Distribución Portable:**
  ```powershell
  .\scripts\build.ps1
  ```
  *Salida generada:* `artifacts/Silen-win-x64/` y `artifacts/Silen-win-x64.zip`.
* **Ejecutar en Desarrollo:**
  ```powershell
  .\scripts\run.ps1
  ```

---

## 6. Módulo Bloom · Asistente IA (Google Gemini API)

Bloom es el asistente y copiloto inteligente de Silen para diagnóstico interactivo y optimización asistida.

### Arquitectura de Bloom:
* **Proveedor:** Google Gemini API (`gemini-2.5-flash` o `gemini-1.5-flash`), endpoint REST oficial `v1beta`.
* **Almacenamiento de clave:** `Silen.Core` debe almacenar la API key cifrada usando `ProtectedData.Protect` (DPAPI de Windows) en `%AppData%\Silen\bloom.dat`.
* **Herramientas (Tool / Function Calling):**
  1. `scan_pc`: Ejecuta diagnóstico mediante `WindowsCollector` y devuelve el snapshot en JSON.
  2. `apply_optimizations(categories, preserve_audio)`: Aplica ajustes vía `NativeOptimizationService` con respaldo previo.
  3. `clean_temp_files()`: Ejecuta limpieza segura de temporales mediante `NativeTemporaryCleanup`.
  4. `undo_last_change()`: Restaura el último cambio registrado en el `OptimizationJournal`.
* **Regla de confirmación:** Bloom NUNCA debe ejecutar `apply_optimizations` sin pedir confirmación explícita al usuario en el chat.
* **Interfaz de usuario:** Pestaña o panel lateral "Bloom" en `MainWindow.xaml` con:
  - Estado inicial: Configuración de API Key con enlace a `https://aistudio.google.com/app/apikey`.
  - Estado activo: Historial de mensajes, entrada de texto y botones rápidos de confirmación.

---

## 7. Lista de Verificación antes de Entregar Código (Definition of Done)

- [ ] ¿Los nuevos ajustes están documentados en español claro y técnico?
- [ ] ¿Cada ajuste almacena su estado original en el `journal` para permitir `Undo`?
- [ ] ¿Se verificó que los tests pasen exitosamente con `dotnet test`?
- [ ] ¿El binario se compila sin errores ni advertencias nuevas?
- [ ] ¿La interfaz respeta el tema sobrio sin romper la accesibilidad ni el teclado?
