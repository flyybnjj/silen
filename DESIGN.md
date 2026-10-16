---
name: Silen Lino
description: Una herramienta tranquila para revisar y ajustar un PC.
colors:
  linen: "#F4F2EC"
  panel: "#F4F2EC"
  ink: "#292B27"
  brick: "#B23B35"
  muted: "#595A55"
  line: "#CFCEC9"
  control-line: "#848580"
  raised: "#EAE8E2"
  selection: "#E0DED8"
  dark-background: "#191A18"
  dark-panel: "#21221F"
  dark-selection: "#363832"
  dark-raised: "#2B2C28"
  dark-text: "#F0EEE7"
  dark-muted: "#BCBDB4"
  dark-line: "#45473F"
  dark-control-line: "#808276"
  dark-brick: "#EA8B80"
typography:
  heading:
    fontFamily: "Source Serif 4"
    fontSize: "34px"
    fontWeight: 600
    lineHeight: "44px"
  summary-heading:
    fontFamily: "Source Serif 4"
    fontSize: "30px"
    fontWeight: 600
    lineHeight: "40px"
  summary-section:
    fontFamily: "Source Serif 4"
    fontSize: "22px"
    fontWeight: 600
    lineHeight: "30px"
  summary-metric:
    fontFamily: "Source Serif 4"
    fontSize: "30px"
    fontWeight: 600
    lineHeight: "38px"
  section:
    fontFamily: "Source Serif 4"
    fontSize: "20px"
    fontWeight: 600
    lineHeight: "28px"
  body:
    fontFamily: "Source Serif 4"
    fontSize: "15px"
    fontWeight: 400
    lineHeight: "24px"
  label:
    fontFamily: "Source Serif 4"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: "20px"
  small:
    fontFamily: "Source Serif 4"
    fontSize: "13px"
    fontWeight: 400
    lineHeight: "20px"
rounded:
  control: "5px"
  inner-control: "4px"
  section: "0px"
spacing:
  related: "4px"
  inline: "8px"
  control-group: "12px"
  group: "16px"
  inset: "20px"
  section: "24px"
  content-inset: "36px"
components:
  button-primary:
    backgroundColor: "{colors.brick}"
    textColor: "{colors.linen}"
    rounded: "{rounded.control}"
    padding: "9px 15px"
    height: "40px"
  button-secondary:
    backgroundColor: "{colors.linen}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "9px 15px"
    height: "40px"
  button-quiet:
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "8px"
    height: "40px"
---

# Sistema de diseño de Silen

## Overview

**Dirección: «Silencio, calma y rendimiento discreto».**

Herramienta nativa de Windows para observar el equipo y revisar cambios concretos.
La precisión y el control del usuario pesan más que la decoración. Lino combina
una serif con carácter, superficies cálidas y un único acento llamado brick.
El modo oscuro conserva esa identidad. La composición de Resumen sirve como
referencia; cada vista debe adaptarse según su propia tarea.

**Lino** es el nombre del tema visual de Silen: base crema cálida, texto tinta y
acento brick; su variante oscura traslada esa misma identidad a grafito y crema.

Características: una sola familia tipográfica, espacio con propósito, líneas finas,
acciones explícitas y detalles técnicos bajo demanda. No se prometen FPS, menos
ruido ni mejoras no medidas.

## Colors

El acento brick identifica acción principal, selección y foco. No es por sí solo
un código de error, peligro ni garantía de protección.
Los neutros linen e ink sostienen la interfaz; muted, line y raised diferencian
metadatos, límites y estados. No asignar un acento distinto a CPU, RAM y GPU.

En la selección, el fondo de filas y texto seleccionado es neutro (selection),
con texto ink. brick se reserva para la marca lateral de navegación, el borde de
una opción seleccionada, el indicador de una casilla y el contorno de foco.
La navegación puede conservar el fondo base y marcar solo la entrada activa.
No rellenar filas enteras con brick ni interpretar su fondo neutro como un error.

El tema oscuro usa los tokens dark-*: grafito cálido, texto crema y brick aclarado
para conservar contraste. ThemeManager.cs es la fuente de verdad de variantes;
Theme.xaml contiene los valores iniciales claros. Usar recursos dinámicos.
Claro, Oscuro y Sistema son preferencias de la app. Alto contraste tiene prioridad
y utiliza los colores de Windows. El texto también comunica los estados.

### Tokens y contraste

Esta tabla refleja los valores de Theme.xaml y ThemeManager.cs;
no introduce tonos nuevos. Los nombres son semánticos: linen corresponde al fondo
WPF Ink; ink, al texto WPF Text. Las variantes oscuras corresponden a los tokens
dark-* del frontmatter.

| Token | Claro | Oscuro | Pareja evaluada | Ratio claro / oscuro |
| --- | --- | --- | --- | --- |
| linen | #F4F2EC | #191A18 | ink sobre linen | 12,77:1 / 15,05:1 |
| panel / dark-panel | #F4F2EC | #21221F | ink sobre panel | 12,77:1 / 13,77:1 |
| ink | #292B27 | #F0EEE7 | ink sobre linen | 12,77:1 / 15,05:1 |
| muted | #595A55 | #BCBDB4 | muted sobre linen | 6,21:1 / 9,21:1 |
| line | #CFCEC9 | #45473F | line frente a linen | 1,41:1 / 1,85:1 |
| raised | #EAE8E2 | #2B2C28 | ink sobre raised | 11,67:1 / 12,11:1 |
| selection / dark-selection | #E0DED8 | #363832 | ink sobre selection | 10,63:1 / 10,23:1 |
| brick | #B23B35 | #EA8B80 | texto de botón sobre brick | 5,25:1 / 7,07:1 |
| control-line | #848580 | #808276 | borde frente a linen | 3,32:1 / 4,47:1 |

panel es la superficie de controles y ayudas emergentes: coincide con linen en
claro y se separa del fondo en oscuro mediante dark-panel. selection es el relleno
neutro de selección en listas, tablas y campos; su variante es dark-selection.
Ambos pares existen en los recursos WPF Panel y Selection. Sus ratios evalúan el
texto sobre la superficie, no la diferencia de luminosidad entre superficies.

El texto del botón usa linen en claro y dark-background en oscuro. Los ratios
se calculan con luminancia relativa sRGB y colores opacos, redondeados a dos
decimales: no acreditan el contraste final sobre BLOOM ni sustituyen comprobar
foco, hover y estados. line solo separa grupos de forma decorativa: no sirve
para texto ni como único límite necesario de un control. Para este último usar
control-line y verificar al menos 3:1; texto normal, al menos 4,5:1.

### Error y acciones destructivas

**Regla de estado explícito.** Un error se reconoce por el rótulo «Error» en
Semibold, una explicación concreta y la acción de recuperación. Usar texto ink, superficie
raised y borde control-line; no un botón brick relleno ni el mismo tratamiento
de la acción principal. Si hace falta un icono, debe ser funcional, coherente
con los trazos existentes y acompañado de texto; nunca un emoji.

Una acción destructiva se presenta con contorno neutro y verbo específico:
«Eliminar definitivamente 2 archivos». Su confirmación explica el alcance y la
ausencia de deshacer; Cancelar conserva el foco inicial y se diferencia de
confirmar. Eliminar no reutiliza el aspecto de «Optimizar PC».

## Typography

Source Serif 4 Regular y Semibold están incrustadas. Es la única familia para
títulos, navegación, botones, datos y tablas; no depende de fuentes instaladas.
El wordmark conserva su geometría original en Brand.xaml. Su fuente original no
está identificada: no sustituir el dibujo por texto compuesto.

Los roles generales son heading, section, body y small. Resumen emplea
summary-heading, summary-section y summary-metric. Etiquetas de lectura: label;
nombre del equipo: 20/28. Las métricas usan cifras tabulares y lining.

Los px del esquema representan unidades lógicas WPF de 1/96 de pulgada, escaladas
por el DPI de Windows. Cuerpo: body; no reducir toda la interfaz a metadatos para
hacerla caber. Limitar las explicaciones a 520–660 unidades. Estados y errores
pueden envolver líneas; nombres truncados conservan acceso al texto completo.

**Regla de una sola voz.** Variar tamaño, peso, proximidad y tono antes de añadir
otra familia. Escribir «1 seleccionado» y «3 seleccionados»; cantidades y verbos
se ajustan con SpanishText, sin la fórmula «(s)».

## Layout

Columna lateral de 205 y contenido con ancho máximo 1120 e inset horizontal 36.
Ventana inicial 1240×820, mínima 1000×680. No hay variante móvil. Título, contenido
desplazable y franjas de estado ocupan regiones distintas; contabilizar estas
franjas antes de fijar la altura de una sección.

La escala observada es 4/8/12/16/20/24. Grupos relacionados estrechos, secciones
separadas con aire y líneas cuando aclaran la estructura. Evitar márgenes
uniformes que den la misma importancia a todos los bloques.

En Resumen: identificación y lecturas → optimización → hallazgos y límites.
Optimizar PC debe verse completo sin desplazar a tamaño mínimo. Al preparar una
selección, Aplicar permanece fuera del scroll de la lista. Un solo desplazamiento.
Identidad completa y fuentes son desplegables; los efectos de ajustes no se esconden.

La detección comienza al abrir. Sin datos se muestra una explicación breve; si
falla, Reintentar. No repetir Analizar en todas las vistas. Conservar orden de
lectura y teclado al adaptar cada tarea.

## Elevation & Depth

Superficies planas y líneas de una unidad lógica, sin sombras decorativas ni vidrio.
Los rellenos de hover, selección y desactivación son neutros. En una selección,
brick corresponde a la marca, al borde de estado o al indicador de casilla; el
foco también usa su contorno. El texto permanece ink sobre el relleno neutro,
salvo el glifo de una casilla rellena con brick, que utiliza texto de contraste.
La navegación no necesita un relleno de selección si su marca lateral y peso
identifican la entrada activa. BLOOM conserva el material de la
referencia y es la única ilustración con volumen. Su desenfoque está detrás de
la interfaz; no se aplica al texto ni se convierte en glassmorphism.

## Shapes

Secciones rectas sin radio; controles con control e inner-control. Los únicos
trazos auxiliares son checks, chevrons y ordenación funcional. No añadir iconos
decorativos ni símbolos Unicode como sustitutos. Wordmark y BLOOM son recursos
independientes; el icono de Windows usa BLOOM.

## Components

Theme.xaml y los estilos locales son la autoridad. El archivo auxiliar
.impeccable/design.json documenta referencias nativas y movimiento; no convierte
WPF en HTML ni acredita su render mediante un navegador.

**Acciones.** Un botón principal brick por etapa: Optimizar PC prepara; Aplicar
selección ejecuta tras confirmar. Secundarios con contorno; auxiliares discretos.
En Resumen estos últimos son planos y de peso normal. Área útil mínima de 40 y
foco visible. No duplicar Aplicar dentro de la lista. Toda acción destructiva
conserva explicación y confirmación. Evitar accesos que solo repiten navegación.

**Campos y listas.** Fondo neutro, borde definido, foco visible y estados de error,
desactivación y carga. No esconder información necesaria tras hover. Conservar
columnas, ordenación y virtualización de tablas. Desplegar no cambia preferencias.

**Navegación.** Texto de 17, selección Semibold con marca lateral de una unidad.
El logo abre Ajustes con nombre accesible y señal visible. Entradas principales:
Resumen, RAM y red, Drivers, Historial y Bloom.

**Lecturas.** Etiqueta, valor y contexto tienen papeles distintos. Las trazas de
Resumen miden 28 de alto y muestran datos reales. No inventar puntuaciones.
No disponible permanece explícito; un promedio terminado no se llama lectura en vivo.

**BLOOM.** Preservar silueta, lámina posterior, pliegue frontal y lóbulo derecho.
Introducción de 1,8 s; ambiente de 8 s, aleteo de 4 s y 24 fps. Pausar al ocultar,
minimizar, desactivar, trabajar o cerrar. Reducir movimiento usa reposo; alto
contraste oculta la ilustración. Estados: reposo, análisis, espera, éxito, atención, error.

**Regla de presencia secundaria.** BLOOM nunca compite con la acción principal:
texto, foco, progreso y botones deben seguir siendo el primer plano legible.
No añadir destellos, cambios de escala, saturación o movimiento para reclamar
atención. Comprobar la pose más visible y el movimiento, no solo una captura
favorable. Si reduce legibilidad o distrae, atenuar su presencia o pausar la
ilustración; no atenuar texto, botones ni estados para compensarla.

## Voz

Frases cortas, literales y tranquilas. Nombrar la acción, el dato observado y el
siguiente paso. Ajustar plurales y verbos a la cantidad. No usar exclamaciones
promocionales, superlativos, promesas de rendimiento ni garantías absolutas.

| Bien | Mal |
| --- | --- |
| «Revisa los ajustes antes de aplicarlos.» | «¡Lleva tu PC al siguiente nivel!» |
| «Se liberaron 2,1 GB.» | «¡Potencia tu PC al máximo!» |
| «No se pudo completar el análisis. Reintenta.» | «Error fatal desconocido.» |
| «Eliminar definitivamente 2 archivos.» | «Hacer una limpieza extrema.» |
| «Punto de restauración creado a las 14:20.» | «Tu PC está 100 % protegido.» |

Los ejemplos con cifras son ejemplos de redacción, no resultados de Silen.
Mostrar espacio liberado solo después de una eliminación comprobada: retirar
archivos a recuperación no libera espacio. «Creado» requiere verificar un punto
nuevo; no sustituye los estados reutilizado, no disponible o fallido.

## Recuperación

**Regla de protección antes del cambio.** Antes de aplicar un lote, solicitar
un punto de restauración mediante el ayudante autorizado y comprobar su resultado
antes de la primera modificación. Mantener además el respaldo previo de cada
ajuste en OptimizationJournal: las dos protecciones tienen alcances distintos.

Mostrar si Windows creó un punto nuevo, reutilizó uno anterior o no pudo crearlo.
No declarar «protegido» por recibir únicamente un resultado booleano de éxito.
Si la protección está desactivada o falla, no empezar los ajustes a escondidas:
ofrecer cancelar, abrir la configuración oficial o continuar solo con el journal
tras una decisión explícita. No cambiar la frecuencia ni activar protección sin
consentimiento. Windows puede reutilizar un punto reciente sin crear otro.
[Comportamiento oficial de SRSetRestorePoint](https://learn.microsoft.com/en-us/windows/win32/sr/calling-srsetrestorepoint).

**Regla de salida visible.** Junto al resultado debe aparecer «Deshacer último
cambio», con el nombre del ajuste respaldado. Reutilizar UndoAsync y sus controles
de permisos y conflictos; confirmar qué se restaurará antes de ejecutar. Sin
respaldo válido, explicar por qué está deshabilitado. Mostrar los resultados
parciales sin afirmar una restauración completa. Deshacer un ajuste no equivale
a restaurar Windows ni a revertir todo un lote.

El punto de restauración no sustituye una copia de archivos personales ni permite
recuperar una purga definitiva. Restaurar Windows es otra operación, con su
alcance y confirmación propios.

## Verificación por vista

Aplicar este checklist a cada vista individual. Marcar solo lo probado con
evidencia; si un estado no corresponde a su tarea, registrar «No aplica» y el
motivo. No heredar la aprobación de Resumen ni marcar funciones propuestas.

- [ ] Carga: indicar qué ocurre; conservar progreso y cancelación cuando existan.
- [ ] Vacío: explicar por qué faltan datos y qué puede hacer el usuario, sin cifras ficticias.
- [ ] Error: problema concreto, recuperación disponible y distinción de la acción principal.
- [ ] Selección: contadores 0/1/varios correctos, disponibilidad y efectos visibles; preparar no aplica.
- [ ] Aplicación: una acción principal por etapa; confirmación y resultado completo o parcial claros.
- [ ] Recuperación: respaldo real, acceso a Deshacer y conflicto/error según lo implementado; indicar las carencias.
- [ ] Claro y oscuro: comprobar texto, bordes, foco, hover y desactivación sobre la composición real.
- [ ] 1240×820 y 1000×680: acción principal completa, orden estable, wrapping y desplazamiento comprensibles.
- [ ] Teclado: foco visible, orden lógico, acceso a controles y detalles completos sin depender de hover.
- [ ] Contenido extremo: nombres largos, datos no disponibles y textos de estado extensos sin pérdida de información.
- [ ] Movimiento: BLOOM en segundo plano; comprobar pausa y reducir movimiento cuando correspondan.
- [ ] Alcance: controles conectados, sin regresiones ni cambios no autorizados en otras vistas.

Cerrar cada vista con capturas de los tamaños y temas probados, resultado de las
comprobaciones funcionales y límites de lo no ejercitado. Una revisión de fuente
no equivale a una prueba manual con lector de pantalla ni a una medición de GPU.

## Do's and Don'ts

- Conservar una sola familia, la paleta y los recursos originales de identidad.
- Reservar brick para la acción principal, selección y foco.
- Mantener jerarquía visible, plurales correctos y frases literales.
- Conservar datos desconocidos, advertencias, confirmaciones y restauración.
- Comprobar 1240×820 y 1000×680, claro/oscuro, carga, error y selección.
- No introducir gradientes, neón, vidrio, brillos ni sombras decorativas.
- No usar cohetes, rayos, escudos, llamas ni emojis como iconos.
- No ocultar la acción principal para ganar aire ni esconder efectos de ajustes.
- No extrapolar una composición a otra vista sin revisar su tarea.

## Estado de adopción

Fecha de revisión: **2026-10-16** (America/Santiago). Las secciones anteriores
definen la especificación. Esta sección concentra qué está adoptado y qué falta;
documentar una regla no acredita que esté activa ni que haya sido probada en UI.

| Área | Estado en la fecha de revisión |
| --- | --- |
| Identidad y tokens | Source Serif 4, Lino, variantes claras/oscuras y brick están en los recursos. Los nombres WPF conservan los alias indicados abajo. |
| Selección | Casillas, navegación y listas verificadas en XAML. Las filas de tabla conservan una marca lateral de 1 unidad al perder foco, además del relleno neutro; comprobadas mediante render WPF en claro, oscuro y alto contraste simulado. En alto contraste la marca usa Highlight del sistema. El relleno nunca es su único indicador. Revisión manual en alto contraste real pendiente. |
| Resumen | Composición refinada y referencia de jerarquía. Sus contenidos se conservan en la entrega de las vistas restantes. |
| RAM y red | Refinada individualmente: dos columnas de lecturas, interpretación y procesos desplegables, una acción principal «Medir DNS» y «Actualizar detalles» secundaria. Se conservan Source Serif 4 y los tokens existentes. Títulos de sección 22/30, lecturas 24/32 con cifras tabulares, cuerpo 15/24 y metadatos 13/20; texto explicativo limitado a 620 unidades. Acción DNS y alcance completos a 1000×680. Estados vacío, pendiente, error local, progreso y cancelación comprobados con servicios simulados. |
| Drivers | Actualizaciones primero: Buscar es principal mientras no haya ofertas; Descargar toma ese papel cuando hay resultados. El estado literal y los controles de Windows Update se conservan. Catálogo OEM, filtros, compatibilidad y fuentes oficiales siguen accesibles; búsqueda por modelo secundaria. Títulos 22/30 y secciones planas. |
| Ajustes | Sin segundo wordmark. Apariencia en un grupo acotado y modo experto separado. Un único Aplicar selección manual fuera del desplazamiento, visible solo con el manual abierto. Temporales, recuperación y Deshacer conservan controles y confirmaciones. Copy distingue retirar de purgar. |
| Historial | Análisis antes que exportaciones; fecha y equipo agrupados sin truncar el nombre. Abrir análisis es principal; Actualizar es secundaria. Guías conservan fecha y resultado; exportar queda al final como detalle, con advertencia de privacidad. |
| Bloom | Mayor espacio para conversación, entrada y confirmación visibles. Proveedor, privacidad, consumo de tokens y eliminación de clave en el desplegable de configuración. Bienvenida con contraseña y dos acciones claras. Burbujas con ancho máximo 620; avisos largos desplazables y error explícito. No cambia almacenamiento, proveedor, herramientas ni permisos. |
| Error y destructivo | Resumen, RAM y red, los avisos generales de las otras vistas y el error local de Bloom reutilizan SummaryErrorNotice sin marca: ErrorText → Text, ErrorLine → ControlLine y ErrorSurface → Raised con rótulo «Error» Semibold. También el estado de error del manual. Los alias globales conservan Accent; el pie de actualización y diálogos ajenos a estas vistas no se consideran adaptados por herencia. No se renombran recursos. |
| Visibilidad del error | Variante sin marca elegida por el usuario el 2026-10-16. Capturas WPF claras/oscuras y texto largo comprobados; no equivale a una prueba manual con lector de pantalla. |
| Recuperación existente | Restauración por journal y UndoAsync disponible en Ajustes; conserva validación de conflictos. |
| Punto previo y Deshacer contextual | Implementados para el lote de Resumen. Preflight mediante ayudante tipado; creado exige ID/fecha y verificación del catálogo. Reutilizado, desactivado o fallido se declaran literalmente y requieren una segunda confirmación antes de continuar con journal. Cancelar no inicia ajustes. Deshacer visible, con nombre, confirmación y revalidación de ID; conserva conflictos y resultados parciales. Pruebas automatizadas con servicios simulados; ensayo real pendiente exclusivamente en VM con snapshot. |
| Alcance de esta revisión | Refinamiento individual completado para RAM y red, Drivers, Ajustes, Historial y Bloom. La observación del análisis guardado permanece en Drivers y se omite en las tareas que no leen ese análisis. Sin cambios al motor, renombrados ni plural de Bloom. El preflight de Recuperación no se extrapola a otras vistas. El usuario excluyó el ensayo en VM de esta entrega; no se ejecutó ni se acredita. |
| Verificación manual | No completada: recorrido de teclado, hover/foco, lector de pantalla, movimiento/reducir animaciones y alto contraste real. No marcar estos puntos por pruebas de código o capturas. Guía: docs/prueba-recuperacion-vm.md. |

### Renombrados pendientes

Propuestas para una revisión de código posterior, no cambios efectuados por este
documento. Cualquier renombrado debe actualizar todas las referencias XAML/C# y
verificarse en ambos temas antes de retirar el alias anterior.

| Recurso WPF existente | Nombre propuesto | Motivo |
| --- | --- | --- |
| Ink | AppBackground | Es el fondo, no la tinta; representa linen en claro y dark-background en oscuro. |
| Text | TextPrimary | Explicita su papel de texto; corresponde al token documental ink. |
| Accent | Brick | Alinea el nombre del recurso con el único acento del documento. |
| White | OnBrick | Es texto de contraste del botón; en oscuro no es blanco. |
| SelectedText | TextOnSelection | Distingue el texto del relleno neutro Selection. |
| MemoryAccent / GpuAccent | MemoryTrace / GpuTrace | Son trazos neutros de lecturas, no acentos de marca adicionales. |

ErrorText y ErrorLine conservan nombres semánticos útiles: los avisos adaptados
usan el componente con alias locales. La retirada de los alias globales que aún apuntan
a Accent queda pendiente de una revisión específica; no sustituirlos por nombres de color. Los tokens documentales
panel, dark-panel, selection y dark-selection no requieren renombrar ahora los
recursos WPF Panel y Selection.

### Verificación de Resumen y Recuperación · 2026-10-16

Registro de evidencia automática, no aprobación manual de la vista. El checklist
general anterior sigue siendo la plantilla para cada revisión.

- [x] Carga y vacío: smoke y diagnóstico; estados no disponibles conservados.
- [x] Error: pruebas WPF y capturas, incluido mensaje largo; aviso sin marca elegido.
- [x] Selección: contadores 0/1/varios y marca de tabla sin foco en tres temas simulados.
- [x] Aplicación: preflight, rechazo, cancelación y continuación explícita con servicios simulados.
- [x] Recuperación simulada: ID confirmado, rechazo, conflicto, parcial y respaldo desaparecido.
- [ ] Recuperación real: crear/verificar punto y deshacer un ajuste dentro de una VM con snapshot.
- [ ] Claro/oscuro/alto contraste: recorrido manual de hover, foco y desactivados; renders automáticos disponibles.
- [x] Tamaños: comprobaciones automáticas a 1240×820 y 1000×680; Deshacer fuera del desplazamiento.
- [ ] Teclado completo y lector de pantalla: pendientes de uso manual.
- [x] Contenido extremo: texto extenso completo mediante desplazamiento y datos desconocidos explícitos.
- [ ] Movimiento: pausa, minimizar y reducir animaciones pendientes de validación manual.
- [x] Alcance del código: sin renombrados; Bloom y su plural pendientes fuera de esta tarea.

### Verificación de RAM y red · 2026-10-16

Evidencia y auditoría: `docs/ui-ram-red-impeccable-2026-10-16.md`. Las capturas
WPF usan datos simulados; no acreditan mediciones de la conexión ni cambios reales.

- [x] Carga, vacío y error: progreso, reintento local, datos anteriores retirados tras fallo y vacío posterior a una medición fallida.
- [x] Estados DNS: pendiente distinto de fallo, «1 fallo», cancelación con resultados parciales y sin declarar ganador.
- [x] Selección: navegación y tablas conservan los indicadores existentes. No aplica un contador de ajustes: esta vista no prepara un lote.
- [x] Acción principal: «Medir DNS» visible y completa al entrar; deshabilitada durante la medición, con Cancelar disponible.
- Aplicación y recuperación: **No aplica**; esta vista consulta memoria y mide DNS, sin cambiar configuración, vaciar cachés ni cerrar procesos. No incorpora preflight.
- [x] Renders en claro, oscuro y paleta de alto contraste simulada, a 1240×820 y 1000×680.
- [x] Contenido extremo: interpretación desplegable completa; estados DNS largos envueltos en dos líneas, sin truncamiento.
- [x] Alcance: comparación contra copia previa; solo vista, presentación, pruebas WPF y condición de cabecera de RAM y red. Motor y contenido de las demás pestañas sin cambios.
- [ ] Hover, foco y recorrido completo con teclado y lector de pantalla: pendientes de uso manual.
- [ ] Alto contraste real de Windows: pendiente; simular colores no activa todas las preferencias del sistema.
- [ ] Movimiento: pausa, minimizar y reducir animaciones pendientes de validación manual; animador sin modificaciones.
- [ ] Recuperación real de Resumen: ensayo en VM con snapshot pendiente, independiente de esta vista.

### Verificación de Drivers, Ajustes, Historial y Bloom · 2026-10-16

Informe y evidencia: `docs/ui-vistas-restantes-2026-10-16.md`. Estas vistas no
heredan la validación de Resumen. Las capturas de interacción usan datos y
servicios simulados, claves ficticias y ninguna llamada a un proveedor de IA.

- [x] Drivers: búsqueda visible a tamaño mínimo; vacío, ofertas, descarga, cancelación y permisos mediante servicios simulados en la batería WPF.
- [x] Ajustes: modo normal/experto, selección independiente y un Aplicar completo al llegar al final del desplazamiento; confirmación y rechazo conservados en pruebas simuladas.
- [x] Historial: análisis antes que exportaciones, nombre largo legible y selección que habilita Abrir. Carga de observación guardada cubierta por la batería existente.
- [x] Bloom: bienvenida, chat vacío, mensaje, propuesta, confirmación, cancelación, error local neutral y eliminación de configuración con almacén simulado. Enviar y confirmar permanecen visibles a tamaño mínimo.
- [x] Claro y oscuro: capturas WPF a 1000×680 y 1240×820 para las cuatro vistas; selección y estados deshabilitados conservan recursos dinámicos.
- [x] Alcance: 59 bindings de comandos conservados, seis pestañas, contenido de Resumen y RAM y red sin cambios. Motor y datos de usuario sin modificaciones.
- Recuperación: **no se añade preflight**. Ajustes conserva journal, Deshacer y recuperación de temporales; Bloom conserva propuestas tipadas y confirmación explícita. Solo se ejercitaron servicios simulados.
- [ ] Vacío de Historial y todos los fallos de carga de datos: no ejercitados visualmente en este pase; mensajes y manejo existentes conservados.
- [ ] Recorrido manual de teclado, hover/foco y lector de pantalla.
- [ ] Alto contraste real y escalado DPI del sistema en las cuatro vistas.
- [ ] Movimiento, minimizar y reducir animaciones: animador conservado, verificación manual no realizada.

El ensayo de Recuperación en VM queda fuera de esta tarea por decisión del usuario;
su ausencia sigue registrada como límite de la evidencia, sin bloquear esta entrega.

