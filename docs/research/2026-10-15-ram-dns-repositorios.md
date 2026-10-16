# Referencias de RAM, DNS y aplicaciones

Consultadas el 15 de octubre de 2026. Estrellas obtenidas de la API pública de
GitHub en esta sesión; no son una certificación de seguridad ni una medición de rendimiento.

| Repositorio | Estrellas | Licencia publicada | Uso en este trabajo |
| --- | ---: | --- | --- |
| [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) | 64.278 | MIT | Referencia de organización de ajustes de Windows y sus consecuencias. |
| [winsiderss/systeminformer](https://github.com/winsiderss/systeminformer) | 16.248 | MIT | Referencia de medición de memoria física, compromiso y procesos. |
| [henrypp/memreduct](https://github.com/henrypp/memreduct) | 10.823 | GPL-3.0 | Referencia evaluada; describe vaciado de cachés con APIs internas. No se incorpora ese mecanismo ni su código. |
| [DNSCrypt/dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy) | 13.739 | ISC | Referencia de resolución DNS y comportamiento de servidores. No se instala un proxy ni un servicio DNS. |
| [miekg/dns](https://github.com/miekg/dns) | 8.782 | BSD-3-Clause | Referencia del protocolo y validación de mensajes DNS; no se añade una dependencia Go. |
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | 139.348 | MIT | Referencia general de herramientas Windows; no es un optimizador de RAM. |

Se descartó Optimizer como referencia principal de mantenimiento: el repositorio
`hellzerg/optimizer` tiene 18.243 estrellas, pero aparece archivado. Los benchmarks
DNS pequeños encontrados no se incorporaron como dependencias ni ejecutables.

## Decisiones concretas

El código nuevo es C# nativo de Silen: no se descargan ni ejecutan utilidades de
estos proyectos. Para RAM se usa `K32GetPerformanceInfo` y la propiedad WMI
`AutomaticManagedPagefile`, solo en lectura. Se observan cinco muestras antes de
hablar de presión sostenida. La reducción de segundo plano usa los ajustes propios
de Silen con journal y se aplica desde Optimizar PC. No se vacía standby ni se
redimensiona la paginación con una fórmula universal.

DNS mide consultas A, no ping. Cada servidor recibe consultas a tres dominios
públicos: microsoft.com, cloudflare.com y wikipedia.org (sus nombres www). El
resultado comprueba transacción, pregunta, códigos, nombres comprimidos y cadena
CNAME/A; usa TCP cuando UDP marca truncado. Ordena respuestas completas antes de
las parciales y muestra mediana, última muestra y fallos. No cambia los DNS del
adaptador y no garantiza menor ping de juegos. La caché del proveedor y la ruta
afectan la medición: se indica que es una observación y debe repetirse.

La localización de productos lee claves Uninstall y manifiestos Steam/Epic;
no consulta `Win32_Product` ni ejecuta launchers/desinstaladores. La búsqueda de
ejecutables se acota a la instalación elegida y omite enlaces y binarios auxiliares
reconocibles. El usuario confirma el ejecutable; la preferencia GPU se respalda.

## Fuentes primarias adicionales

- [Microsoft: GetPerformanceInfo](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo).
- [Microsoft: tamaño del archivo de paginación](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows).
- [Microsoft: consultas y búsquedas DNS](https://learn.microsoft.com/en-us/windows-server/networking/dns/queries-lookups).
- [Cloudflare: direcciones 1.1.1.1](https://developers.cloudflare.com/1.1.1.1/ip-addresses/).
- [Google Public DNS](https://developers.google.com/speed/public-dns/docs/using).
- [Quad9: direcciones y características](https://quad9.net/service/service-addresses-and-features/).
- [Sordum: DNS Jumper](https://www.sordum.org/7952/dns-jumper-v2-3/).
