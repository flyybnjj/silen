# Vistas restantes · refinamiento de Silen

Fecha: 2026-10-16. Alcance autorizado: Drivers, Ajustes, Historial y Bloom,
con el mismo tratamiento de tipografía, composición y tono aplicado a RAM y red.
Se conservan DESIGN.md, Source Serif 4, paleta, mascota y animador. El usuario
excluyó el ensayo en VM de esta tarea; no se realizó ni se acredita.

## Auditoría inicial

Las líneas anteriores corresponden a
`artifacts/ui-restantes-baseline/src/Silen.App/MainWindow.xaml`.

| Área | Severidad | Archivo:línea anterior | Problema | Cambio aplicado |
| --- | --- | --- | --- | --- |
| Drivers | Medio | MainWindow.xaml:179 | Estado de Windows Update, acciones y búsqueda compiten como bloques del mismo peso. | Actualizaciones primero; estado literal de Windows Update conservado debajo, con acciones secundarias. |
| Drivers | Bajo | MainWindow.xaml:181 y :193 | Buscar actualizaciones y buscar por modelo carecen de una jerarquía común. | Buscar actualizaciones es principal sin ofertas; Descargar toma el papel con ofertas. Modelo queda secundario. |
| Ajustes | Medio | MainWindow.xaml:210 | Se repite el wordmark y el primer bloque consume el espacio útil de la ventana mínima. | Se elimina solo el wordmark interior y se acota el grupo de apariencia. |
| Ajustes | Medio | MainWindow.xaml:229 | Aplicar manual está dentro de un desplazamiento largo; desaparece al recorrer las opciones. | Único Aplicar fuera del scroll, con el resumen de su selección. |
| Historial | Medio | MainWindow.xaml:249 | Exportar antecede a la tarea habitual de abrir una observación. | Análisis primero, guías después y exportación desplegable al final. |
| Historial | Bajo | MainWindow.xaml:251 | Nombre de equipo truncado y fecha, nombre y cantidad distribuidos como tres columnas rígidas. | Fecha y nombre agrupados; nombre envuelto y disponible también en tooltip. |
| Bloom | Medio | MainWindow.xaml:298–300 | Pie de estado y consumo reduce el chat a unas pocas líneas; Status se limita a una altura sin scroll. | Consumo y privacidad en configuración; estado en presencia, compositor y avisos con desplazamiento. |
| Bloom | Bajo | MainWindow.xaml:265 | El aviso de cuotas menciona solo Google aunque existe selección de proveedor. | Texto coherente con el proveedor elegido, sin prometer gratuidad. |

## Implementación

- `src/Silen.App/MainWindow.xaml:3`: roles locales compartidos por las cuatro vistas: títulos 22/30 Semibold, cuerpo 15/24 y metadatos 13/20 existentes. Secciones planas con separadores de una unidad y espaciado 8/12/16/20/24.
- `src/Silen.App/MainWindow.xaml:184`: prioridad de búsqueda y descarga; se conservan salud real de Windows Update, permisos, catálogo OEM, filtro, compatibilidad y origen de cada paquete.
- `src/Silen.App/MainWindow.xaml:214`: preferencias sin wordmark duplicado; modos, perfiles y respaldos existentes. Aplicar manual queda fuera del scroll, sin duplicarlo. El texto distingue retirar archivos a recuperación de purgarlos definitivamente.
- `src/Silen.App/MainWindow.xaml:256`: análisis por delante de exportación, selección explícita y una acción principal para abrir. No cambia almacenamiento ni contenido de informes.
- `src/Silen.App/MainWindow.xaml:262`: bienvenida más directa, chat con espacio, burbujas de hasta 620 unidades, configuración desplazable y confirmación explícita conservada. El campo de contraseña y su vaciado al guardar permanecen intactos.
- `src/Silen.App/BloomViewModel.cs:70`: HasError expone el estado ya existente para elegir el aviso visual; sin modificar peticiones, claves, herramientas ni permisos.
- Avisos generales de las vistas restantes y error manual reutilizan SummaryErrorNotice, sin marca lateral, con rótulo Error y paleta neutral. No se renombran recursos ni se altera el pie del actualizador por herencia.

## Verificación y revisión visual

La prueba inicial falló por la falta de espacio en el chat a 1000×680; después
se comprobó un área de lectura de al menos 120 unidades con Enviar completo.
También se comprueban la acción de Drivers en el primer viewport, la prioridad
de los análisis en Historial y Aplicar manual completo al final del scroll.

El primer pase de capturas incluyó las cuatro vistas en claro y oscuro a dos
tamaños, bienvenida, propuesta y error. Detectó una doble separación en Guías y
copy impreciso sobre temporales. Un único lote de polish corrigió estos puntos;
la confirmación visual posterior es el cierre, sin ciclos de retoque adicionales.

La revisión independiente de fuente no encontró defectos concretos nuevos en
bindings, nombres, comandos, advertencias ni confirmaciones. No ejecutó acciones
del sistema ni sustituye una prueba manual de accesibilidad.

Evidencia:

- `artifacts/ui-restantes-tests-before.log`: batería .NET 10 anterior, 633 pruebas correctas, una OEM opcional omitida y ningún fallo.
- `artifacts/ui-restantes-tests-after.log`: batería posterior con los mismos resultados: 633 correctas, una OEM opcional omitida y ningún fallo.
- `artifacts/ui-restantes-red/status.json`: reproducción de falta de espacio en Bloom.
- `artifacts/ui-restantes-final-build.log`: compilación final con cero errores y cero advertencias.
- `artifacts/ui-restantes-final/interaction-checks.json`: 245 comprobaciones WPF correctas con servicios simulados, sin mutaciones del sistema.
- `artifacts/ui-restantes-final/interaction-fixtures/remaining-*.png`: temas, tamaños y estados específicos.
- `artifacts/ui-restantes-scope.json`: solo tres archivos de código cambiados; Resumen y RAM y red iguales al baseline, seis pestañas y los mismos 59 bindings de comandos.
- Detectores type/layout: sin resultados; el escáner no analiza XAML y no acredita calidad o accesibilidad. Se revisó el render nativo.
- `scripts/verify-portable.ps1:1`: parámetro acotado de tiempo de espera para la batería ampliada de capturas. El valor predeterminado sigue siendo 60 segundos; esta entrega usa 120. No afecta al rendimiento ni a los timeouts de la app.
- `artifacts/ui-restantes-portable-verification.json` y `artifacts/ui-restantes-desktop.json`: verificación efectiva de paquete y copia al escritorio, conservando la versión anterior.

El portable extraído arrancó y completó 245 comprobaciones; su diagnóstico fue
de lectura y registró `AppliedSystemChanges: false`. Se verificaron los 33
archivos del ZIP y de la copia al escritorio. El manifiesto ECDSA fue verificado;
no constituye firma Authenticode. La preversión actual está en
`C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`, con el acceso directo
Silen actualizado y la versión anterior conservada. Las 19 capturas específicas
están reunidas en `artifacts/ui-restantes-2026-10-16/`.

## Checklist por vista

| Verificación | Drivers | Ajustes | Historial | Bloom |
| --- | --- | --- | --- | --- |
| Claro/oscuro y dos tamaños | Capturas | Capturas | Capturas | Capturas |
| Acción principal | Buscar / Descargar | Aplicar manual único y fijo | Abrir análisis seleccionado | Enviar / Confirmar según etapa |
| Vacío | Batería de ofertas vacías | Selección vacía en batería existente | Mensaje conservado; sin render específico | Bienvenida y chat vacío |
| Error/cancelación | Batería existente de red y permisos simulados | Batería existente de rechazo, conflicto y recuperación simulada | Fallo de lectura no ejercitado en este pase | Error local, propuesta y cancelación simulados |
| Texto largo | Compatibilidad y datos conservados, tabla desplazable | Opciones y efectos conservados | Nombre de equipo envuelto | Avisos desplazables |
| Recuperación | Sin nuevo preflight | Journal, Undo y archivos retirados existentes | No aplica: lectura de observaciones | Herramientas tipadas y confirmación existentes |

- [x] Compilación y pruebas automatizadas; ninguna optimización real durante la verificación.
- [x] Controles y confirmaciones conectados; acceso a datos oficiales y advertencias conservados.
- [x] Documentación de adopción actualizada.
- [ ] Teclado completo, hover/foco y lector de pantalla manuales.
- [ ] Alto contraste real, DPI y todas las combinaciones de estados extremos en las cuatro vistas.
- [ ] Movimiento, minimizar y reducir animaciones manuales; animador sin cambios.

No se añaden nuevas optimizaciones, preflight, descargas automáticas ni dependencias.
Los fixtures no representan mejoras de rendimiento medidas ni pruebas reales de
los cambios de Windows.
