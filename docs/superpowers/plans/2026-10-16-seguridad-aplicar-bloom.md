# Ejecución del plan aprobado de seguridad y funcionalidad

Autorización: auditoría del 16 de octubre y solicitud «continúa implementa todo».
Se conserva el código no confirmado y el rename existentes; no se crean commits
ni se publica una release externa. Cambios en grupos verificables, sin aplicar
ajustes al PC ni reiniciarlo durante las pruebas.

- [x] Baseline .NET 10: 546 pruebas correctas, una OEM optativa omitida.
- [x] A: plazo total IA, contexto/costos acotados, diagnóstico mínimo con
  consentimiento, fachada de credenciales, contratos neutrales y proveedores.
- [x] B: journal privilegiado protegido y ejecutor elevado de acciones tipadas;
  UI/IA sin elevación, autenticación de IPC, límites y pruebas de falsificación.
- [x] C1: selección manual pendiente, confirmación y Aplicar en cada optimizador;
  resultados por paso y clasificación inmediata/reabrir/cerrar sesión/reiniciar.
- [x] C2: reinicio ahora o al cerrar, elección visible y revocable, sin forzar
  cierre de programas ni ejecutar reinicios reales en las pruebas.
- [x] C3: limpieza recuperable con manifiesto antes de mover, validación de
  identidad/rutas/enlaces y purga separada confirmada. No atribuir espacio libre
  a archivos que siguen en cuarentena.
- [x] D: manifiesto firmado con clave pública incorporada, fallar cerrado sin
  firma; herramientas de firma, lock transitivo y controles de seguridad en CI.
- [x] E: tema oscuro y alto contraste, tokens, foco/nombres accesibles, estados de Bloom,
  pausa de mantenimiento UI y mediciones de reposo sin fingir costo GPU.
- [x] Informes: omitir comandos de inicio en diagnósticos/exportaciones.
- [x] Suite completa, build, interacciones WPF y portable extraído sin SDK.
- [x] Revisión final independiente, documentación y portable actualizado en
  artifacts/escritorio conservando versión anterior.

Distribución de trabajo: IA/Bloom, helper/journal y firma/CI tienen propietarios
independientes. La integración de MainWindow/MainViewModel, Aplicar/reinicio,
limpieza y temas corresponde al coordinador. No hay escrituras superpuestas.

La implementación de firma no equivale a un certificado Authenticode público:
se comprueba autenticidad mediante manifiesto de release firmado. Los proveedores
usan endpoints HTTPS fijos y claves DPAPI separadas; Gemini conserva la clave y
modelo de la instalación anterior.

Verificación final: 621 pruebas correctas, 1 OEM optativa omitida; 198 comprobaciones WPF, 33 archivos portable verificados y copiados al escritorio. Compilación sin errores ni advertencias. Evidencia y límites en docs/verificacion-seguridad-aplicar-bloom.md.
