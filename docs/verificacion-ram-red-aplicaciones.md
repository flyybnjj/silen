# Verificación de RAM, red y selección de aplicaciones

15 de octubre de 2026. SDK .NET 10.0.402 del entorno de Codex.

Se sustituyó Hardware por RAM y red. Se retiraron la pestaña Gaming y el perfil
Fortnite de la interfaz; sus respaldos históricos conservan compatibilidad.
Optimizar PC reúne los ajustes generales y la preferencia GPU por ejecutable
elegido. Se localizan productos con registro Uninstall y manifiestos Steam/Epic,
con selección manual para productos no detectados. Esta preferencia no configura
los gráficos internos de todos los juegos ni garantiza un aumento de FPS.

RAM muestra memoria física, disponible, compromiso y límite. La evaluación exige
cinco lecturas consecutivas; reinicia la ventana tras fallos o pausas mayores de
cinco segundos. La paginación se consulta, sin cambiarla. No se vacían cachés ni se
cierran procesos. Las opciones de segundo plano existentes siguen en Optimizar PC.

DNS realiza consultas válidas a tres dominios por servidor, UDP con fallback TCP
si hay truncamiento. Presenta mediana, última respuesta y fallos. Los resultados
incompletos no se recomiendan como ganador. No modifica adaptadores ni mide ping
de juegos. Las referencias y licencias están en
[la investigación](research/2026-10-15-ram-dns-repositorios.md); no se añadieron
dependencias ni ejecutables externos para estas funciones.

## Evidencia final

- `dotnet test Silen.sln -c Release --no-restore`: 516 pruebas correctas,
  cero fallos y una prueba OEM en vivo omitida por ser optativa.
- `scripts/build.ps1 -SkipTests`: compilación con cero advertencias y cero errores;
  publicación autocontenida win-x64.
- `scripts/verify-portable.ps1`: ZIP extraído en ruta nueva con espacios, ejecución
  sin SDK en PATH, 33 archivos verificados y 174 comprobaciones WPF correctas.
  `AppliedSystemChanges=false`, `CapturedGaming=false`,
  `UniversalOptimizationReady=true`. Se renderizaron las seis vistas.
- Regresiones verificadas: bucles de nombres DNS rechazados, cadena CNAME válida,
  cambio de ejecutable durante la revisión sin aplicar al destino no revisado,
  reinicio de muestras RAM tras interrupción y estado final de medición DNS.
- Revisión independiente de los cambios anteriores, sin hallazgos pendientes.
- Lectura real del equipo: 16 GB físicos, paginación automática y 78 productos
  detectados. Hubo respuestas DNS válidas de los servidores actuales y Cloudflare;
  las rutas virtuales sin respuesta se indicaron como fallos. No se aplicaron
  optimizaciones reales durante las verificaciones.

Registros locales: `artifacts/universal-final-tests.log`,
`universal-final-build.log`, `universal-final-portable.json`,
`universal-final-desktop.json`. La captura mínima de DNS y las pruebas de controles
están en la carpeta de evidencia indicada por el JSON del portable.

ZIP SHA256:
`30423EC75FC0DFFA777BD6F65CFB202E39B31D8B4E380CAE2FBBEDD75B3D9894`.

El acceso directo `C:\Users\avalo\Desktop\Silen.lnk` apunta a
`C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`; se verificaron los 33
archivos copiados. Ejecutable SHA256:
`26F81CA0A059EAF2D0DE70C34433611142452A6920D905348416193761AE658A`.
La versión anterior se conservó en una carpeta separada del escritorio.
