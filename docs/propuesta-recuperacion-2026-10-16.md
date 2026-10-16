# Recuperación · plan aprobado y estado de implementación

Estado al 2026-10-16: **aprobado e implementado para Resumen**, con pruebas de
integración simuladas. El ensayo real de creación y restauración sigue pendiente
en una VM con snapshot; ver [procedimiento](prueba-recuperacion-vm.md).
El texto siguiente conserva el diseño aprobado. Silen no activa Protección del
sistema, no altera su frecuencia ni restaura todo Windows. El preflight no se
amplía en esta tarea a Bloom ni al optimizador manual.

## Antes de aplicar

La revisión previa indicaría: «Antes de aplicar, Silen solicitará a Windows un
punto de restauración. Cada ajuste seguirá guardando su estado anterior».
La confirmación autoriza esa operación; UI y Bloom permanecen sin elevar.

El ayudante existente recibiría una acción tipada específica de creación de
punto, con descripción generada localmente y sin aceptar comandos, rutas ni
instrucciones de IA. Usaría la API nativa SRSetRestorePoint antes de la primera
mutación del lote. Se conserva el journal previo a cada escritura.

La interfaz debe distinguir estos resultados verificados:

| Estado | Presentación y decisión |
| --- | --- |
| Creado | Identificador y fecha del nuevo punto; después comienza el lote confirmado. |
| Reutilizado | Windows devolvió un punto anterior: mostrar su fecha y aclarar que no creó uno nuevo. Permitir cancelar o continuar expresamente. |
| Protección desactivada | No empezar los ajustes. Ofrecer abrir la configuración oficial o continuar expresamente solo con respaldos de Silen. No activarla a escondidas. |
| No disponible o fallo | Explicar el error; cancelar por defecto. Continuar solo tras una nueva decisión informada. |
| Cancelación | No iniciar ajustes pendientes. No afirmar que una llamada nativa ya iniciada pudo interrumpirse ni que el punto fue eliminado. |

No cambiar SystemRestorePointCreationFrequency para saltarse la política de
Windows. SRSetRestorePoint puede devolver éxito y el identificador de un punto
anterior si existe uno reciente; el éxito booleano no acredita creación nueva.

## Después de aplicar

En el bloque de resultado de Resumen aparecería **Deshacer último cambio**, junto
al nombre concreto del ajuste. Antes de ejecutar: «¿Restaurar Modo Juego a su
estado anterior?». Despacha el UndoAsync existente con el ID respaldado revisado.

- Sin respaldo verificado: deshabilitado, con explicación; nunca inventar un undo.
- Cambio privilegiado: consultar el journal protegido y solicitar permiso cuando
  corresponda. No interpretar un journal editable como autorización elevada.
- Conflicto externo: conservarlo y explicar por qué no se restauró; no forzar.
- Resultado parcial: mostrarlo como parcial y ofrecer revisar sus respaldos.
- Tras restaurar: refrescar disponibilidad, resultado y último respaldo pendiente.

El botón deshace **un cambio**, no todo Windows ni una optimización completa de
varios pasos. Deshacer un lote completo exigiría agrupar explícitamente sus IDs
y revisar el resultado individual; no se prometería sin ese soporte.

Restaurar todo Windows sería una acción diferente, con explicación de alcance
y confirmación en la herramienta oficial; Bloom no la iniciaría automáticamente.
El punto de restauración complementa el journal y no sustituye una copia de
archivos personales ni permite recuperar una purga definitiva de temporales.

## Cierre y validación propuestos

El resultado debe distinguir lo aplicado de una selección aún pendiente. Si todos
los pasos elegidos ya están aplicados, no presentar otra aplicación como necesaria.
Se propondría actualizar su estado y conservar acceso al resultado y a Deshacer.

PRs pequeños: (1) contrato y preflight del punto con estados reales; (2) integración
previa al lote y confirmación de excepciones; (3) acceso contextual al Undo ya
existente y claridad del cierre. Pruebas con ayudante/API simulados: creado,
reutilizado, desactivado, fallo, cancelación antes de mutar, conflictos de undo y
resultados parciales. Pruebas reales de restauración solo en un entorno desechable.

## Fuentes oficiales consultadas

- [Calling SRSetRestorePoint](https://learn.microsoft.com/en-us/windows/win32/sr/calling-srsetrestorepoint): inicio/fin de cambio y reutilización por frecuencia.
- [Protección del sistema](https://support.microsoft.com/en-us/windows/experience/backup-recovery/system-protection): disponibilidad, configuración y alcance.
- [Restoring the System](https://learn.microsoft.com/en-us/windows/win32/sr/restoring-the-system): requisitos y comportamiento de restauración.
