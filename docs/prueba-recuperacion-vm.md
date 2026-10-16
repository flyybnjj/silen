# Ensayo de Recuperación en una VM con snapshot

Fecha: 2026-10-16. **Pendiente de ejecución manual.** Las pruebas automáticas
usan servicios simulados. No acreditan una llamada SRSetRestorePoint real,
la interacción UAC ni una restauración efectiva de Windows.

Este procedimiento se ejecuta solo en una VM desechable Windows 11 x64. No
se debe sustituir la VM por el PC principal. Un snapshot del hipervisor y
un punto de restauración de Windows son mecanismos distintos.

## Preparación

1. Preparar una VM Windows 11 x64 sin datos personales ni claves de Bloom.
   Copiar el portable completo, comprobar su hash y conservar el paquete original.
2. Crear un snapshot del hipervisor llamado «Silen antes de Recuperación» y
   comprobar que se puede volver a él. Preparar además un estado con Protección
   del sistema desactivada para el escenario correspondiente.
3. Abrir Silen de forma normal. Su interfaz no debe ejecutarse como administrador.
   Elegir únicamente Modo Juego para la primera prueba; no seleccionar servicios,
   energía, limpieza ni perfiles de aplicaciones.
4. Registrar la fecha, zona horaria, versión de Windows, versión/hash de Silen,
   tema y estado previo de Modo Juego. Conservar capturas y resultados dentro de
   la VM. La lectura siguiente no cambia preferencias:

   ```powershell
   Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\GameBar' -Name AutoGameModeEnabled -ErrorAction SilentlyContinue
   Get-CimInstance -Namespace root/default -ClassName SystemRestore |
       Select-Object SequenceNumber, CreationTime, Description
   ```

## Casos y criterios de aceptación

| Caso | Acción manual en la VM | Resultado requerido |
| --- | --- | --- |
| Rechazo inicial | Preparar y cancelar la primera confirmación. | Sin UAC de restauración, sin punto solicitado y sin cambio de Modo Juego. |
| Cancelar UAC | Confirmar el lote y rechazar el permiso de Windows. | Solicitud cancelada; el lote no empieza ni se ofrece continuar automáticamente. |
| Inicialización del ayudante | Ejecutar el caso de creación en la VM y revisar cualquier fallo antes de continuar. | El ayudante arranca sin WPF y prepara seguridad COM antes de WMI. Un fallo, incluido RPC_E_TOO_LATE, detiene el preflight; no se ignora para anunciar éxito. |
| Punto creado | Con Protección del sistema activa y sin punto reciente según la política vigente, confirmar el lote. | El ID/fecha aparecen en el catálogo, con descripción local de Silen. El mensaje dice «creado y verificado» y luego se aplica el ajuste. |
| Punto reutilizado | Repetir con un punto reciente existente. | Dice literalmente «Windows reutilizó… No creó uno nuevo». La segunda confirmación muestra Cancelar enfocado. Rechazar conserva el ajuste anterior. |
| Reutilización consentida | Confirmar expresamente «Continuar solo con journal». | Solo entonces empieza el lote; la pantalla conserva la advertencia y la elección. |
| Protección desactivada | Volver al estado de VM preparado con protección desactivada y confirmar el lote. | Mensaje literal de desactivación o indisponibilidad comprobada; no anuncia «protegido». El ajuste sigue intacto hasta una segunda confirmación explícita. |
| Abrir configuración | Usar «Abrir Protección del sistema». | Abre la herramienta oficial. Silen no activa protección ni modifica frecuencia por su cuenta. |
| Deshacer rechazado | Tras aplicar, pulsar Deshacer y cancelar. | Sin modificación ni pérdida del respaldo. |
| Deshacer confirmado | Confirmar el nombre del cambio mostrado. | Solo ese ajuste recupera su estado anterior; se verifica su valor y se actualizan disponibilidad y respaldo pendiente. |
| Conflicto externo | En la VM, cambiar el mismo valor respaldado a un valor diferente del anterior y del aplicado antes de deshacer. | Se conserva el valor externo. Se muestra el conflicto y sigue disponible el respaldo. |
| Cierre o cancelación | Cancelar durante la comprobación o cerrar la UI. | No se inicia el lote después de cancelar. Una llamada nativa ya iniciada puede terminar y conservar su punto; Silen no promete interrumpirla ni lo elimina. |

Si Windows reutiliza un punto reciente, eso no prueba el caso «creado». Para
probar creación usar una imagen de VM preparada sin punto reciente o esperar
la frecuencia vigente. **No cambiar SystemRestorePointCreationFrequency ni
borrar puntos para forzar un resultado.** La aplicación tampoco lo hace.

La prueba de conflicto puede usar un valor de DWORD distinto solo dentro de la
VM; guardar antes el valor exacto y volver al snapshot al terminar. No ejecutar
esa manipulación en un equipo real ni añadir comandos arbitrarios al ayudante.

## Accesibilidad y temas: comprobación manual pendiente

- [ ] Recorrer Resumen completo solo con Tab/Shift+Tab y activar con teclado.
- [ ] Confirmar que Cancelar recibe el foco al ofrecer continuar sin punto nuevo.
- [ ] Revisar hover, foco y desactivados en claro, oscuro y alto contraste real de Windows.
- [ ] Seleccionar una fila de tabla, mover el foco fuera y comprobar su marca persistente.
- [ ] Verificar mensajes largos con lector de pantalla, sin depender del tooltip.
- [ ] Reducir animaciones en Windows; comprobar reposo de BLOOM y pausa al ocultar/minimizar.
- [ ] Ejecutar los casos reales de la tabla y adjuntar sus IDs, valores y capturas.
- [ ] Volver al snapshot y comprobar el estado inicial.

No distribuir como «validado en Windows real» hasta completar estos registros.
Deshacer restaura un ajuste del journal; no restaura todo Windows ni todo el lote.
No se incorpora una orden de restauración completa de Windows en Silen.

## Fuentes

- [SRSetRestorePoint y frecuencia/reutilización](https://learn.microsoft.com/en-us/windows/win32/sr/calling-srsetrestorepoint).
- [STATEMGRSTATUS y errores nativos](https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/ns-srrestoreptapi-statemgrstatus).
- [SystemRestore: identificación y fecha](https://learn.microsoft.com/en-us/windows/win32/sr/systemrestore).
- [Seguridad COM requerida por System Restore](https://learn.microsoft.com/en-us/windows/win32/sr/using-system-restore).
