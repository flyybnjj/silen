# RAM y red · auditoría y refinamiento

Fecha: 2026-10-16. Alcance: una sola vista, siguiendo DESIGN.md y las operaciones
typeset, layout y quieter de Impeccable. Las capturas emplean datos simulados.
No se aplicaron ajustes de Windows ni se incorporó el preflight de Recuperación.

## Auditoría inicial

Referencias a la copia anterior conservada en `artifacts/ram-red-baseline/`.

| Prioridad | Evidencia anterior | Problema | Cambio |
| --- | --- | --- | --- |
| Media | RamNetworkView.xaml:5–9 | Una cifra grande seguida de lecturas y explicación apiladas dominaba la primera pantalla; DNS quedaba debajo del pliegue a tamaño mínimo. | Dos columnas de lecturas y explicación desplegable; DNS y su alcance visibles al entrar. |
| Media | RamNetworkView.xaml:3 y :12 | Dos superficies con peso similar y acciones poco diferenciadas. | Secciones planas, separación fina; Actualizar detalles secundaria y Medir DNS principal. |
| Media | RamNetworkView.xaml:14 y :18 | Método y limitaciones extensos repartidos alrededor de la tabla; lectura inicial costosa. | Alcance breve siempre visible y método completo en «Qué se mide y qué no». |
| Media | RamNetworkViewModel.cs:12–13 y RamNetworkView.xaml:16 | La ausencia de consultas podía parecer un fallo; tabla vacía sin siguiente paso. | Pendiente diferenciado, mensaje vacío con acción concreta y estado posterior al fallo distinto del inicial. |
| Baja | RamNetworkViewModel.cs:14 | «1 fallos» en progreso. | Singular mediante SpanishText.Count; el plural de Bloom queda fuera. |

## Decisiones aplicadas

- `src/Silen.App/RamNetworkView.xaml:3`: títulos 22/30, lecturas 24/32 con cifras tabulares, cuerpo y metadatos existentes; una familia y los mismos tokens.
- `src/Silen.App/RamNetworkView.xaml:16`: lecturas comparables en dos columnas; datos secundarios con menor peso.
- `src/Silen.App/RamNetworkView.xaml:19`: interpretación conservada en un desplegable, con ancho máximo de 620 unidades.
- `src/Silen.App/RamNetworkView.xaml:27`: Medir DNS como acción principal de la tarea; «No cambia tus DNS ni mide el ping de juegos» queda visible.
- `src/Silen.App/RamNetworkView.xaml:34`: estados largos con wrapping y altura de fila variable; sin perder columnas ni resultados.
- `src/Silen.App/RamNetworkViewModel.cs:70`: errores locales de detalles retiran datos anteriores y permiten reintentar.
- `src/Silen.App/RamNetworkViewModel.cs:91`: fallo al descubrir resolutores libera el estado de medición; cancelación conserva resultados parciales sin declarar ganador; progreso tardío se descarta por generación.
- `src/Silen.App/MainWindow.xaml:101`: solo en RAM y red se oculta la observación del diagnóstico guardado. La hora de lectura propia sigue visible.

Se conservan proveedores, consultas, validación, límites, proceso de medición y
motor de memoria existentes. Medir DNS hace consultas de red cuando se usa en la
app; las pruebas de interacción sustituyen ese servicio por uno simulado.

## Crítica final y polish

La jerarquía distingue lecturas, interpretación y acción. La información frecuente
se ve primero y el detalle mantiene acceso explícito. El copy describe lo que se
mide sin prometer menor ping. Los estados de espera, fallo y cancelación ya no se
confunden. El primer pase visual detectó alcance DNS debajo del pliegue y celdas
cortadas: una única corrección de espacio y wrapping resolvió ambos; el pase de
confirmación verificó los resultados en renders WPF.

Límites: las tablas requieren desplazamiento en la ventana mínima; el fondo de
Bloom, el pie global y el animador se conservan. La paleta de alto contraste
simulada no sustituye el tema real del sistema. No se asigna una puntuación de
accesibilidad a partir de capturas ni se da por validada la interacción manual.

## Evidencia

- `artifacts/ram-red-tests.log`: .NET 10, 633 pruebas correctas, 1 prueba OEM opcional omitida, 0 fallos.
- `artifacts/ram-red-final-build.log`: compilación con 0 errores y 0 advertencias.
- `artifacts/ram-red-latest.txt`: ruta de la ejecución WPF final; 235 comprobaciones correctas en `interaction-checks.json`.
- `src/Silen.App/InteractionSmokeChecks.cs:1201`: fixtures de RAM, DNS, error, cancelación y tamaños; sin mutaciones de Windows.
- `artifacts/ram-red-scope.json`: cuatro archivos de código cambiados frente al respaldo; seis TabItem conservados. Los únicos cambios son vista, ViewModel, pruebas y condición de cabecera.
- `artifacts/ram-red-type-detect.json`: detector de tipografía sin resultados; no analiza XAML, por lo que no constituye validación de la vista.
- Revisión independiente de fuente contra baseline: sin hallazgos concretos nuevos; no ejecutó acciones del sistema ni pruebas manuales.

Capturas finales para revisión: `artifacts/ram-red-2026-10-16/`.
Verificación del paquete y copia al escritorio: `artifacts/ram-red-portable-verification.json`
y `artifacts/ram-red-desktop.json`. El portable extraído arrancó, completó el diagnóstico
de lectura y pasó 235 comprobaciones; `AppliedSystemChanges` fue false. Se comprobaron
los 33 archivos contra el ZIP y contra la copia del escritorio. El manifiesto ECDSA
se verificó; no constituye firma Authenticode. La versión anterior se conservó.

## Verificación por vista

- [x] Carga, vacío inicial y posterior al fallo, error local y reintento.
- [x] Pendiente, resultados, progreso y cancelación parcial; singular «1 fallo».
- [x] Acción y alcance visibles a 1000×680; composición comprobada también a 1240×820.
- [x] Capturas en claro, oscuro y paleta de alto contraste simulada.
- [x] Interpretación extensa desplegable y celdas de estado sin truncamiento.
- [x] Alcance de código comparado con respaldo; otras vistas y motor sin cambios.
- Selección de ajustes, aplicación y Deshacer: **No aplica**, vista de diagnóstico y medición; sin lote ni acciones del sistema.
- [ ] Hover, foco, recorrido completo de teclado y lector de pantalla reales.
- [ ] Alto contraste real de Windows.
- [ ] Pausa, minimizar y reducir movimiento comprobados manualmente.
- [ ] Recuperación de Resumen en VM con snapshot; ver `prueba-recuperacion-vm.md`.

La siguiente vista requiere aprobación del usuario.
