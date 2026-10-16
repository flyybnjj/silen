# Silen

## Product

Aplicación nativa de Windows 11 x64, en español, para analizar recursos y drivers, aplicar ajustes concretos de Windows y preferencias por juego o aplicación elegida, y conservar respaldos para restaurarlos. Distribución portable autocontenida con .NET 10 y WPF. Optimizar PC reúne las acciones principales, incluidas las entradas de inicio elegidas y el control de navegadores en segundo plano; RAM y red muestra mediciones del equipo y consultas DNS reales.

## Intent confirmed by the user

Silencio, calma y rendimiento discreto. La app presenta lo observado y acciones concretas; no promete aumentos de FPS ni una reducción de ruido medida. Frases cortas, humanas y concretas.

## Approved identity

El usuario confirmó el nombre Silen y eligió la dirección Lino con detalles en rojo. Colores base: lino `#F4F2EC`, tinta `#292B27`, rojo ladrillo `#B23B35`. Después pidió acercar toda la tipografía a las letras del logo y mejorar la composición general. La interfaz utiliza una sola familia serif: Source Serif 4 Regular y Semibold, incrustada. El wordmark es la pieza gráfica serif en minúsculas «silen» aportada por el usuario el 2026-10-15; se conserva como vector, sin el marco ni la etiqueta del ejemplo. Su fuente original no está identificada; la familia de interfaz aproxima su carácter sin sustituir el dibujo.

Navegación lateral de texto y una zona principal amplia. El diagnóstico comienza al abrir y Resumen presenta el equipo antes del optimizador. Se retiraron los botones repetidos de análisis; solo aparece reintento cuando falta el diagnóstico. El logo abre Ajustes: allí Modo experto permite mostrar el Optimizador manual. Limpieza, restauración y perfiles de temporizador permanecen accesibles en modo normal.

La figura orgánica roja BLOOM, creada por el usuario con ChatGPT, es la mascota y presencia visual de la IA de la app. Conservar su forma de la referencia, sin una letra de apertura ni un morph desde la «s». Aparece al iniciar y permanece desenfocada detrás de la interfaz, con un movimiento tipo mariposa: los laterales se pliegan y abren lentamente mientras flota. El usuario solicitó el 2026-10-15 un chat real con Gemini y clave personal cifrada con DPAPI. Tras reportar un fallo de acceso al modelo inicial 2.5 Flash, se corrigió a Gemini 3.5 Flash-Lite y se comprobó con la misma clave guardada. Bloom propone solo herramientas locales permitidas y requiere confirmación explícita para optimizar, limpiar o restaurar; no simula respuestas de IA cuando falta conexión o clave.

## Constraints

Sin gradientes morados/azules, neón, glassmorphism en controles, sombras decorativas, cohetes, rayos, escudos, llamas ni emojis como iconos. La mascota conserva el material rojo translúcido de la imagen aportada; el resto de la interfaz usa superficies planas. Espacio libre, bordes de 1 px, navegación clara y composición sobria. Preservar funciones, datos existentes, confirmaciones y restauración. Los cambios de diseño no ejecutan ajustes del sistema.

## Platform

La revisión aprobada del 16 de octubre añade proveedor/modelo intercambiable para Bloom (Gemini, OpenAI o Anthropic), consentimiento previo para diagnóstico mínimo y presupuesto por conversación. Cada optimizador aplica su selección confirmada. Los temporales se retiran a recuperación local con purga separada. Solo el ayudante de acciones tipadas se eleva; UI e IA permanecen sin permisos de administrador. Los cambios aplicados ofrecen reinicio ahora o al cerrar, visible y cancelable. El actualizador exige un manifiesto ECDSA firmado. Ver [alcance y verificación](docs/verificacion-seguridad-aplicar-bloom.md).

Windows / WPF, C# / .NET 10. Mantener controles utilizables por teclado y respetar las preferencias de animación de Windows.
