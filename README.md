# Silen

Diagnóstico y ajustes de rendimiento para Windows 11 x64. En español, portable y sin instalar .NET.

**[Descargar Silen](https://github.com/flyybnjj/silen/releases/latest/download/Silen-win-x64.zip)**

## Uso

1. Extrae el ZIP completo y abre **Silen.exe**. Conserva la carpeta **Archivos** a su lado.
2. Espera el diagnóstico automático de tu equipo.
3. Abre **Optimizar PC**, revisa los cambios y confirma tu selección.

## Incluye

- Diagnóstico de CPU, GPU, RAM, discos y temperaturas disponibles.
- Drivers desde Windows Update y fuentes oficiales.
- Ajustes de Windows y aplicaciones, con respaldo y restauración según el tipo de cambio.
- Lecturas de RAM, comparación de DNS y limpieza con recuperación local.
- Bloom: asistente opcional con tu propia clave de IA.

El resultado depende del equipo y su carga; no se garantizan aumentos de FPS. La purga de temporales es irreversible.

Si vienes de FlyyOptimizer, instala este portable en una carpeta nueva. Silen conserva el historial y los respaldos existentes.

## Desarrollo

Requiere el SDK de .NET 10.

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

[Guía de uso](docs/user-guide.md) · [Privacidad de Bloom](docs/bloom.md) · [Licencias](THIRD-PARTY-NOTICES.md)
