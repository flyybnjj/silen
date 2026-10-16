# Third-party notices

Silen utiliza bibliotecas .NET y componentes publicados por sus respectivos autores. Se conservan los avisos y licencias del paquete publicado en la distribución. Las versiones concretas quedan fijadas en los proyectos y en los archivos de dependencias de NuGet.

## LibreHardwareMonitorLib

- Proyecto: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- Paquete NuGet: LibreHardwareMonitorLib 0.9.6.
- Licencia principal: Mozilla Public License 2.0 (MPL-2.0).
- El proyecto contiene partes con licencias adicionales: consultar LICENSE y THIRD-PARTY-NOTICES.txt del repositorio y los avisos del paquete.
- Código fuente y licencia: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE
- No se modificó el código de esta biblioteca.

## System.Management y .NET

- System.Management: https://github.com/dotnet/runtime
- Licencia: MIT, con avisos de terceros de Microsoft.
- La distribución portable incluye runtime .NET y Windows Desktop de Microsoft. Sus archivos LICENSE y ThirdPartyNotices acompañan los componentes publicados.
- System.Security.Cryptography.ProtectedData 10.0.13: cifrado DPAPI de la clave de Bloom, licencia MIT de .NET. Fuente: https://github.com/dotnet/dotnet. Licencia en `licenses/ProtectedData-10.0.13/`.

## Google Gemini API

Bloom realiza solicitudes REST al servicio de Google cuando el usuario envía un
mensaje o confirma una propuesta. No distribuye un SDK de Google ni una clave
compartida. Gemini y AI Studio están sujetos a los términos y cuotas del proveedor:
https://ai.google.dev/gemini-api/terms. La configuración usa una clave del usuario.

## Dependencias transitivas de sensores

Los avisos originales y metadatos de los paquetes se incluyen en `licenses/`. El archivo `.deps.json` registra las dependencias de la distribución.

| Componente | Versión | Licencia | Fuente |
| --- | --- | --- | --- |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | https://github.com/Blacktempel/BlackSharp |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp | 2.6.4 | Apache-2.0 | LICENSE.txt incluido en licenses/HidSharp-2.6.4 |
| Mono.Posix.NETStandard | 1.0.0 | Avisos del paquete y Microsoft/Mono | https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0 |
| System.Management | 10.0.13 | MIT | https://github.com/dotnet/dotnet |
| System.IO.Ports | 10.0.3 | MIT | https://github.com/dotnet/dotnet |
| System.CodeDom | 10.0.13 | MIT | https://github.com/dotnet/dotnet |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT | https://github.com/dotnet/runtime |

No se modificó el código de estas dependencias. Los metadatos `.nuspec` incluidos identifican también las revisiones de código fuente de los paquetes cuando el autor las proporciona.

## PresentMon — referencia histórica de desarrollo

- Proyecto: https://github.com/GameTechDev/PresentMon
- Consola standalone x64 oficial 2.6.0, con licencia MIT incluida en `licenses/PresentMon-2.6.0-MIT.txt` y procedencia/hash en `assets/PresentMon/` del proyecto.
- El código histórico y las pruebas de captura permanecen en el repositorio. La distribución actual no incluye el ejecutable ni ofrece captura o comparación de sesiones.
- SHA-256 fijado: `B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF`. Binario firmado por Intel, obtenido de la release oficial.

## Referencias de diseño

UniGetUI (MIT) se consultó como referencia sin incluir su código. Optimizer (GPL-3.0) fue una referencia histórica; no se incluyó su código.

## Selección WinUtil para Fortnite

- Proyecto: https://github.com/ChrisTitusTech/winutil, commit `07ccd8e2e755a706f31569808b31f5b77acad6a9`.
- Se incorpora el objeto de configuración `WPFToggleGameMode` de `config/tweaks.json` en `assets/WinUtil/fortnite-winutil.json`, embebido para uso local y copiado con su procedencia a `tools/WinUtil/`.
- Copyright (c) 2022 CT Tech Group LLC. Licencia MIT íntegra en `licenses/WinUtil-MIT.txt`.
- Silen valida la selección y aplica sus dos preferencias HKCU mediante su backend nativo con respaldo del estado original. No incorpora ni ejecuta los scripts o la interfaz de WinUtil. Los demás ajustes del perfil Fortnite son implementación de Silen.

Lenovo Legion Toolkit (GPL-3.0) se consultó como referencia de identificación y acceso al servicio oficial Lenovo. La implementación de Silen se escribió independientemente; no se copió ni incluyó código o binarios de Toolkit. Las respuestas del catálogo y las descargas proceden de Lenovo; la app abre los enlaces oficiales y no distribuye los instaladores.

## Herramientas de desarrollo

xUnit, xunit.runner.visualstudio y Microsoft.NET.Test.Sdk se usan para pruebas y no se incluyen en la aplicación publicada. Sus avisos están disponibles en los paquetes NuGet y repositorios oficiales.

## Source Serif 4

La interfaz utiliza Source Serif 4 Regular y Semibold, de Adobe, bajo la SIL Open Font License 1.1. Se incluyen las fuentes originales sin modificar y su licencia en `licenses/SourceSerif4-OFL.txt`. El wordmark de Silen conserva el trazado original de la referencia aportada por el usuario; su fuente de origen no está identificada. Source Serif 4 aproxima su carácter en los textos, controles y tablas.

Fuente: https://github.com/adobe-fonts/source-serif/tree/80d3f8894c09c937bebfa9011247d2e1c79fd6f4
