# Silen.Windows — implementación y verificación

Implementado `net10.0-windows`, con referencia exclusiva a los contratos de Core. La adquisición corre fuera del hilo de interfaz. Las fuentes fallidas dejan notas legibles y conservan los demás datos.

## API pública

- `WindowsCollector.CollectAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)`.
- `DriverUpdateService.FindUpdatesAsync(CancellationToken cancellationToken = default)`.
- `WindowsActions.Open(ActionKind action, string? url = null)`.
- `WindowsActions.ManufacturerUrl(string manufacturer)`.

## Datos y privacidad

WMI: sistema operativo, fabricante/modelo, CPU/GPU, unidades lógicas locales/extraíbles, inventario de drivers y dispositivos PnP. La unión de `Win32_PnPSignedDriver` con `Win32_PnPEntity` preserva dispositivos sin driver y códigos de problema. Los ID de instancia se utilizan solamente durante la unión; el snapshot guarda únicamente ID genéricos PCI/USB/HID sin segmento de instancia/serie.

Se leen Run/RunOnce de HKCU/HKLM (32/64 bits) y `Win32_StartupCommand`, sin consultar la propiedad User. Los comandos anonimizan el perfil actual, otras rutas `Users` y hosts UNC, incluyendo nombres con espacios. No se exportan rutas ejecutables de procesos; se guardan nombre, PID y memoria del conjunto de trabajo de los veinte procesos con mayor memoria. Red guarda descripción, estado y velocidad, sin IP ni MAC. No se consultan nombre de usuario, hostname ni números de serie en el inventario WMI.

`GetSystemTimes` y `GlobalMemoryStatusEx` entregan diez muestras, aproximadamente una por segundo. Las lecturas ausentes, inválidas o no finitas quedan en `null`. El plan activo y su nombre se leen mediante PowrProf y se libera la memoria nativa de la consulta. Las capacidades/estados estáticos tienen tipos no anulables en Core; una unidad sin tamaño/espacio se omite con nota en vez de inventar una medida.

## Sensores y permisos

Paquetes estables consultados directamente en la metadata oficial de NuGet el 13 de octubre de 2026:

- `LibreHardwareMonitorLib` **0.9.6**, proyecto oficial LibreHardwareMonitor, MPL-2.0.
- `System.Management` **10.0.13**.

Se inspeccionó el código de la etiqueta v0.9.6 antes del escaneo real. En esta versión, `Computer.Open` inicializa SMBios, mutexes y OpCode; no utiliza el antiguo Ring0/KernelDriver. Los proveedores de CPU usan PawnIO: `LoadModuleFromResource` abre un dispositivo existente y carga bytecode mediante DeviceIoControl. El escaneo normal mantiene deshabilitados CPU, motherboard, controller, storage y power monitor, para evitar esa ruta de kernel y cualquier instalación/elevación.

Las temperaturas CPU solo se leen de proveedores WMI **ya existentes** en `root\LibreHardwareMonitor` o `root\OpenHardwareMonitor`, con SensorType Temperature e identificadores `/intelcpu/` o `/amdcpu/`. Nunca se presenta una zona térmica ACPI como temperatura CPU. No se inicia ni instala el programa proveedor. Si no existe o deja de responder, se conserva `null` y se informa la limitación.

GPU usa las API de los drivers AMD/NVIDIA ya instalados a través de LHM. La configuración efímera por defecto deja el control de ventiladores en Undefined; no se llama a ninguna operación de control. Intel GPU queda sin sensor LHM porque ese proveedor requiere inicializar la CPU. Los recursos LHM/WMI se cierran al finalizar o cancelar el escaneo.

Fuentes inspeccionadas: [Computer.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Computer.cs), [PawnIo.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/PawnIo/PawnIo.cs), [Control.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Control.cs), [NvidiaGpu.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Gpu/NvidiaGpu.cs), [AmdGpu.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Gpu/AmdGpu.cs).

## Windows Update y acciones

WUA usa `BeginSearch` con `Type='Driver' AND IsInstalled=0 AND IsHidden=0`, callback COM de automatización DISPID 0 y un worker dedicado. La API pública tiene límite de dos minutos y cancelación; el worker solicita RequestAbort y hace CleanUp fuera del callback. La limpieza nativa de una consulta abortada puede continuar en el worker después de que el llamador reciba la cancelación/timeout, sin bloquear la interfaz. No se descarga ni instala nada. WUA ofrece DriverVerDate pero no una propiedad estructurada de versión; Version queda vacía y no se deduce del título. Ausencia de ofertas no se interpreta como inexistencia de otras versiones del fabricante.

Las acciones aceptan solamente los destinos fijos ms-settings y Taskmgr.exe del directorio de Windows. Los fabricantes conocidos usan HTTPS y una lista de hosts exactos para NVIDIA, AMD, Intel, Dell, HP, Lenovo, Asus, Acer, MSI y Microsoft. Se rechazan esquema HTTP/file, host engañoso, credenciales, puerto no estándar, comando arbitrario y sustitución de un destino de Configuración por una URL personalizada.

Documentación: [Search](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search), [BeginSearch](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-beginsearch), [operaciones asíncronas WUA](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/guidelines-for-asynchronous-wua-operations), [IWindowsDriverUpdate](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdate).

## Evidencia de verificación

SDK utilizado: `C:\Users\avalo\.codex\runtimes\dotnet-10-sdk\dotnet.exe`.

`dotnet test tests/Silen.Windows.Tests/Silen.Windows.Tests.csproj -c Release --no-restore`: **60 correctas, 0 fallidas**, unos 14,7 segundos. Se observaron los fallos antes de implementar normalización, validación de acciones, cancelación, exclusión de kernel, identificadores de sensores CPU y redacción de nombres con espacios.

El test de integración real es de solo lectura y no abre ajustes ni inicia una consulta de red. Resultado: 239 dispositivos/drivers, una unidad, siete entradas de inicio, veinte procesos y 49 interfaces de red. Diez lecturas de CPU, memoria y temperatura GPU. Temperatura CPU ausente y conservada como desconocida. La comprobación de privacidad no encontró el perfil actual en comandos exportados.

Se ejecutó además una consulta WUA real mediante un console probe temporal con cancelación a 45 segundos, fuera de la suite. Completó antes del límite con **Success=true, UpdateCount=3**. Solo se guardaron éxito, cantidad y mensaje en `artifacts/windows-driver-probe-result.json`; no se instaló ni descargó ninguna oferta.

`dotnet build src/Silen.Windows/Silen.Windows.csproj -c Release --no-restore`: **0 advertencias, 0 errores**. La integración y ejecución de la suite completa de solución quedan a cargo del agente raíz.
