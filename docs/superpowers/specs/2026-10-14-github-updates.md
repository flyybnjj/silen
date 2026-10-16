# Actualizaciones de Silen desde GitHub

El usuario quiere que la aplicación reciba las versiones publicadas en su repositorio público. Eligió **descargar automáticamente y pedir reiniciar**. La nueva versión incluirá también la corrección de permisos de Edge ya verificada en el commit `2cef6fc`.

## Flujo

La app normal consulta al arrancar la última release estable de `flyybnjj/flyyoptimizer`, sin autenticación, sin bloquear el inicio. Si existe una versión superior, descarga automáticamente el archivo exacto `Silen-win-x64.zip`. Muestra progreso y, después de comprobarlo, un botón **Reiniciar y actualizar**. Nunca reinicia sin pulsar el botón. El botón queda deshabilitado mientras una operación de optimización o análisis esté activa.

Se muestra la versión instalada y un control discreto para buscar de nuevo. Un error de red conserva la app utilizable y ofrece reintento; una respuesta inválida nunca significa «actualizado». En los modos CLI de diagnóstico/prueba no se inicia la consulta automática. Cerrar la app cancela y espera la descarga activa. La descarga completa y validada puede reutilizarse tras volver a abrir.

## Contrato y confianza

Solo se admiten versiones numéricas estables, más nuevas que la instalada, metadata de la API oficial del repositorio, un asset con nombre exacto y tamaño acotado, URL HTTPS del mismo repositorio y digest `sha256:` de 64 caracteres hexadecimales. No se admiten drafts, prereleases, assets duplicados ni destinos ajenos. La descarga se transmite con límite, plazo y cancelación, se comprueban tamaño real y SHA-256 y se publica en caché únicamente tras verificar ambos.

El instalador verifica de nuevo la release concreta y su hash antes de reemplazar. La integridad depende de HTTPS, del repositorio oficial y del digest de GitHub; no equivale a una firma Authenticode independiente. No se incorporan tokens, claves privadas ni credenciales.

## Instalación portable

El ejecutable actual integrado se copia a una carpeta temporal privada de actualizaciones y se ejecuta como ayudante con un trabajo acotado, validado y protegido con DPAPI para el usuario actual. No se requiere instalar otra herramienta ni ejecutar PowerShell. El ayudante espera al proceso original, comprueba su identidad cuando aún existe y aplica únicamente el EXE y `Archivos` dentro de la instalación correspondiente. Una ruta de trabajo ajena a la caché prevista, una instalación de raíz, una redirección o una ruta UNC se rechazan.

El ZIP solo puede contener la carpeta `Silen-win-x64`, con `Silen.exe` y `Archivos`. Se rechazan traversal, rutas absolutas, ADS, enlaces, duplicados con distinto uso de mayúsculas, exceso de tamaño/cantidad y extracción fuera del destino. El archivo principal debe tener cabecera de ejecutable y su FileVersion debe coincidir con la versión publicada. Un bloqueo exclusivo de la instalación y una nueva comprobación del hash del ejecutable anterior evitan que dos ayudantes apliquen trabajos incompatibles. La copia instalada anterior se conserva durante el reemplazo; una excepción permite restaurarla. Si hay archivos bloqueados o faltan permisos, se conserva/restaura la anterior y se informa del fallo. Los cambios ya aplicados y los informes de `%LocalAppData%\Silen` quedan fuera de la instalación y no se borran.

Se vuelve a abrir el EXE principal tras aplicar. El ayudante no eleva permisos ni altera las protecciones de Windows. La preparación y la aplicación rechazan sesiones elevadas; el usuario debe volver a abrir normalmente o instalar manualmente. La caché elimina de forma conservadora paquetes y trabajos antiguos reconocidos, con enumeración acotada; archivos bloqueados o desconocidos se conservan. No se publica una actualización que requiera tokens para descargarla.

## Entrega

Versión **1.1.0**, portable con los mismos dos elementos principales. Los usuarios de **1.0** necesitan instalar esta versión una vez manualmente; las versiones futuras podrán llegar por la actualización integrada. Se publicará el ZIP comprobado en una release nueva, conservando 1.0.

## Verificación

Pruebas con HTTP simulado para versiones, errores, hosts y hashes; ZIPs hostiles y límites; aplicación y restauración en directorios de prueba; estados de UI y cierre mientras hay descarga; ninguna prueba de diagnóstico cambia las preferencias de Windows ni instala sobre la app del usuario. Verificación del portable extraído sin SDK y revisión de código antes de publicar. Prueba real de consulta pública sin token contra GitHub, sin sustituir la instalación en uso.
