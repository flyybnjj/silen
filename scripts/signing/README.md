# Firma de publicaciones de Silen

El actualizador exige `Silen-release.json` junto a `Silen-win-x64.zip`. La firma ECDSA P-256/SHA-256 autentica repositorio, tag, nombre, tamaño y SHA-256 del ZIP con la clave pública incorporada al ejecutable. Se verifica al consultar, antes de preparar la descarga y nuevamente en el ayudante antes de instalar. HTTPS y el digest de GitHub siguen siendo obligatorios. No reemplaza una firma Authenticode ni acredita un certificado de Windows.

En la máquina local del editor, ejecutar una sola vez:

```powershell
.\scripts\sign-release.ps1 -Action init
```

La clave privada se guarda cifrada mediante DPAPI para el usuario de Windows actual en `%LOCALAPPDATA%\Silen\Signing\release-key.dpapi`, fuera del repositorio. Solo la clave pública se escribe en `src/Silen.Windows/UpdateSigningPublicKey.pem`; revisar e incluir ese archivo en el código antes de compilar la versión que confiará en futuras actualizaciones. La herramienta no reemplaza claves existentes. Respaldar la clave cifrada junto con una copia recuperable del perfil de Windows del editor; DPAPI no permite trasladarla a otro usuario como archivo aislado. La pérdida de la clave exige distribuir manualmente una nueva versión con nueva clave pública: no existe una ruta de confianza automática alternativa.

Para crear y comprobar una publicación local:

```powershell
.\scripts\build.ps1 -SignRelease -ReleaseTag v1.2.0
.\scripts\verify-portable.ps1 -RequireSignedRelease -ReleaseTag v1.2.0
```

También se puede firmar un ZIP ya compilado con `sign-release.ps1 -Action sign -Tag v1.2.0`. Distribuir ZIP y manifiesto juntos con ese tag estable en GitHub Releases queda a cargo del editor. Los scripts no publican ni consultan credenciales. Sin clave pública configurada, Silen bloquea las actualizaciones automáticas; sin clave privada válida, no se genera una publicación firmada. Las pruebas crean claves efímeras propias que nunca se incorporan al producto.

La restauración normal usa `packages.lock.json`; la publicación single-file usa `packages.portable.lock.json` en cada proyecto para fijar también `Microsoft.NET.ILLink.Tasks`, agregado por el SDK. El build y la publicación exigen sus respectivos locks sin regenerarlos. Al actualizar dependencias o el SDK de `global.json`, regenerar y revisar ambos grafos explícitamente con `dotnet restore Silen.sln --force-evaluate` y `dotnet restore src/Silen.App/Silen.App.csproj -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true --force-evaluate`.
