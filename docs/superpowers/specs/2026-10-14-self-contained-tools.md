# Silen autocontenido y acciones útiles

El usuario pide que Mejoras/Gaming hagan acciones útiles como WinUtil/Optimizer y que solo tenga que instalar Silen. Se conserva WPF y la distribución .NET self-contained. Se integra la consola oficial standalone de PresentMon, con versión/hash/licencia fijados y comprobación local; no se ejecutan WinUtil/Optimizer ni se solicita al usuario conseguir herramientas externas.

Primera tanda propia: Modo Juego, capturas Xbox y animaciones como controles separados; seleccionar un plan de energía existente; habilitar/deshabilitar entradas Run del usuario actual, conservando su estado; limpiar archivos temporales del usuario con vista previa y selección. Cada ajuste muestra estado y resultado, guarda su estado previo exacto y permite deshacer con comprobación de conflictos. No se llama restoredefaultschemes ni se fuerza un plan universal de rendimiento en portátiles.

La limpieza solo toca archivos de al menos 24 horas bajo LocalAppData/Temp, sin recorrer reparse points; revalida ruta/tamaño/fecha antes de eliminar, omite archivos bloqueados y no anuncia deshacer de archivos eliminados. Se eligen archivos en la interfaz, no se inicia limpieza al abrir o analizar.

El análisis y los smoke tests siguen siendo de lectura del equipo. Las pruebas de mutación usan proveedores de fixture y directorios temporales propios. Los controles de optimización usan el mismo bloqueo/cancelación/cierre de OperationLifetime que las otras operaciones. Gaming resuelve PresentMon incluido, ofrece procesos actuales y elimina el formulario que pide buscar un ejecutable externo. La captura ETW puede necesitar permisos de Windows, lo cual no es una instalación adicional.

No se incorpora aún un gestor general de actualización de otras apps: WinGet no se asume disponible y no será requisito para analizar, optimizar o capturar. Drivers conservan sus fuentes oficiales y su instalación independiente según hardware.

La prueba real del colector encontró acceso denegado de ETW en este equipo. Ante ese error y solo si el token actual no está elevado, Gaming ofrece abrir el ejecutable propio de Silen como administrador mediante un clic explícito. Se conserva la ventana original, no se abre otra herramienta ni se inicia UAC automáticamente. Cancelar el diálogo de Windows muestra un resultado comprensible y permite reintentar.

## Dirección visual añadida por el usuario

El usuario solicita continuar y añadir animaciones en todas las secciones, cambiar la tipografía y darle estilo Apple. Esta preferencia reemplaza la conservación de la paleta oscura anterior: superficies claras, bordes suaves, espacio, acento azul y tipografía Segoe UI Variable de Windows. Se conserva el comportamiento nativo WPF, sin distribuir fuentes SF de Apple ni añadir runtimes web. Las transiciones de páginas, tarjetas y controles usan animaciones WPF cortas, respetan la preferencia Windows de reducir animación y no animan continuamente datos de diagnóstico ni mantienen movimiento en reposo. Los renders de verificación esperan a que termine la transición.
