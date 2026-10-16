# Bloom · asistente interactivo

Bloom permite elegir Gemini, OpenAI o Anthropic. Cada proveedor utiliza su propia clave personal, endpoint HTTPS fijo y catálogo de modelos admitidos. Guardar o seleccionar una configuración no hace solicitudes de pago; la conexión se comprueba al enviar el primer mensaje. No se cambian modelos ni proveedores automáticamente ante errores.

| Proveedor | Modelo inicial | Endpoint |
| --- | --- | --- |
| Gemini | `gemini-3.5-flash-lite` | `https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent` |
| OpenAI | `gpt-5-mini` | `https://api.openai.com/v1/chat/completions` |
| Anthropic | `claude-haiku-4-5-20251001` | `https://api.anthropic.com/v1/messages` |

El catálogo incluye alternativas explícitas verificadas en documentación oficial. Su disponibilidad depende de la cuenta, región y permisos del proveedor. Gemini conserva 3.5 Flash-Lite como predeterminado y la clave existente: no se vuelve silenciosamente a 2.5 Flash, cuyo acceso ya había fallado con la cuenta del usuario.

## Uso y privacidad

1. Selecciona proveedor y modelo y pulsa **Usar proveedor y modelo**. El estado activo muestra la selección aplicada.
2. Abre la página oficial de claves del proveedor o pega una clave en el campo protegido. **Guardar clave cifrada** aplica también la selección visible antes de guardar la clave.
3. Escribe una consulta y envíala. Los mensajes se envían al proveedor elegido; evita escribir información privada.
4. Revisa cada propuesta y confirma o cancela en Silen. Las instrucciones del modelo y un «sí» escrito en el chat no sustituyen este control.

`scan_pc` también necesita autorización local **antes de recopilar**. Solo se comparte un DTO con número de núcleos, RAM, alimentación, métricas medias, temperaturas, recuentos de discos con poco espacio, problemas de drivers y estado de Windows Update. Se excluyen el ID y fecha del snapshot, fabricante/modelo/SKU, nombres/PID de procesos, nombres/comandos de inicio, etiquetas/rutas de discos, hardware IDs, nombres de interfaces y notas libres. El snapshot completo permanece en Silen.

El chat solo vive en memoria. La interfaz recibe `IBloomSession`, que ofrece acciones y estados de credenciales; nunca recupera el valor almacenado de la clave. La única entrada de texto de clave es el guardado desde el campo de contraseña.

## Credenciales y configuración

DPAPI `CurrentUser` cifra las claves mediante `ProtectedData.Protect`. Se conservan el formato, la entropía y la ruta heredada de Gemini, sin migración destructiva:

- Gemini: `%AppData%\Silen\bloom.dat`.
- OpenAI: `%AppData%\Silen\bloom-openai.dat`.
- Anthropic: `%AppData%\Silen\bloom-anthropic.dat`.
- Proveedor/modelo, sin secretos: `%AppData%\Silen\bloom-settings.json`.

Cambiar de proveedor no copia su clave a otro. **Eliminar clave** elimina solo la credencial del proveedor activo; no la revoca en el servicio. Los archivos temporales de guardado contienen únicamente cifrado. Las claves se envían mediante cabeceras, nunca en URLs, journal, mensajes ni logs. Los errores HTTP no muestran cuerpos del proveedor ni excepciones internas que puedan contener secretos. DPAPI vincula el secreto al usuario de Windows; el cifrado del archivo no protege contra procesos que ya ejecutan con sus permisos.

## Herramientas locales

| Herramienta | Comportamiento |
| --- | --- |
| `scan_pc` | Pide autorización antes de recopilar y enviar el resumen agregado descrito arriba. |
| `apply_optimizations` | Prepara pasos concretos del catálogo local: red, gaming y servicios opcionales. Exige confirmación por acción, preserva audio y registra cada original en `OptimizationJournal`. |
| `clean_temp_files` | Previsualiza un lote acotado de temporales de al menos 24 horas. Confirma cantidad/tamaño y los retira a recuperación local; permite restaurarlos desde Ajustes. No libera espacio hasta purgar desde la interfaz con confirmación explícita. Revalida identidad y omite archivos nuevos o cambiados. |
| `undo_last_change` | Propone restaurar el último respaldo pendiente. Exige confirmación y mantiene el bloqueo si existe conflicto externo. |

Las categorías y parámetros se validan localmente con lista positiva. El proveedor no puede enviar rutas, comandos, servicios arbitrarios ni desactivar seguridad o anti-cheat. Cada llamada necesita su propia confirmación y fija los IDs revisados. Otra vista no puede sustituir la selección confirmada ni el lote de limpieza.

## Límites y errores

Cada petición tiene un plazo total de 90 segundos que incluye lectura del cuerpo después de las cabeceras. La cancelación del usuario se propaga a HTTP y herramientas locales. No existen reintentos automáticos; un fallo o cancelación descarta llamadas/propuestas incompletas para evitar repetir mutaciones.

- Entrada: 4000 caracteres por mensaje.
- Contexto: 48 mensajes y 64 000 caracteres serializados.
- Salida: 2048 tokens solicitados; respuesta HTTP máxima de 2 MiB y texto máximo de 24 000 caracteres.
- Conversación: 60 000 tokens informados por el proveedor; al agotarse no se ejecutan nuevas herramientas.
- Lote: cuatro llamadas por respuesta, con seis rondas como máximo.

El consumo se muestra como tokens de entrada/salida. No representa una factura: las cuotas y cargos dependen del proveedor. Nueva conversación descarta el contexto y reinicia su presupuesto. Los estados visibles distinguen reposo, análisis, espera del modelo, éxito, atención y error.

## Verificación

Las pruebas utilizan HTTP simulado, streams que bloquean después de enviar cabeceras, ejecutores simulados y DPAPI sobre archivos temporales aislados. Cubren endpoints/cabeceras, traducción de llamadas y resultados, consumo, plazo total, límites de contexto, presupuesto, consentimiento del diagnóstico, minimización de datos, parámetros adversariales, confirmación individual y ausencia de repetición tras un fallo. No leen la clave real, consumen cuota ni modifican Windows durante la verificación de este cambio.

## Referencias oficiales consultadas el 16 de octubre de 2026

- [Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite), [GenerateContent REST](https://ai.google.dev/api/generate-content).
- [OpenAI GPT-5 mini](https://developers.openai.com/api/docs/models/gpt-5-mini), [GPT-5.4 mini](https://developers.openai.com/api/docs/models/gpt-5.4-mini), [Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create), [Function Calling](https://developers.openai.com/api/docs/guides/function-calling).
- [Anthropic Messages](https://platform.claude.com/docs/en/api/messages/create), [modelos y uso de herramientas](https://platform.claude.com/docs/en/claude_api_primer).
- [ProtectedData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata).

