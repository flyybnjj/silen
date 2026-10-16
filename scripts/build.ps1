param([switch]$SkipTests, [switch]$NoPublish, [ValidatePattern('^[a-fA-F0-9]{40}$')][string]$SourceRevisionId, [switch]$SignRelease, [string]$ReleaseTag)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ($SignRelease -and (-not $ReleaseTag -or $NoPublish)) { throw 'La firma requiere publicación local y -ReleaseTag con la versión estable.' }
$taskWinUtil = Join-Path $taskRoot 'assets\WinUtil\fortnite-winutil.json'
if (-not (Test-Path -LiteralPath $taskWinUtil -PathType Leaf) -or (Get-FileHash -LiteralPath $taskWinUtil -Algorithm SHA256).Hash -ne '354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B') { throw 'La selección integrada de WinUtil falta o fue alterada. Recupera el recurso fijado; el build no descarga ni ejecuta scripts.' }
$taskDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$taskBundled = Join-Path $env:USERPROFILE '.codex\runtimes\dotnet-10-sdk\dotnet.exe'
if (Test-Path -LiteralPath $taskBundled) { $taskDotnetPath = $taskBundled }
elseif ($taskDotnet) { $taskDotnetPath = $taskDotnet.Source }
else { throw 'Instala el SDK .NET 10 desde https://dotnet.microsoft.com/download/dotnet/10.0' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $taskRoot
try {
    & $taskDotnetPath restore Silen.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Las dependencias no coinciden con packages.lock.json.' }
    if (-not $SkipTests) {
        & $taskDotnetPath test Silen.sln -c Release --no-restore --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0) { throw 'Las pruebas fallaron.' }
    }
    & $taskDotnetPath build Silen.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
    if (-not $NoPublish) {
        $taskOutput = Join-Path $taskRoot 'artifacts\Silen-win-x64'
        $taskArtifacts = [System.IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts'))
        $taskStaging = Join-Path $taskArtifacts ('portable-staging-' + [Guid]::NewGuid().ToString('N'))
        $taskPublishProperties = @('-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=embedded', '-p:RestoreLockedMode=true')
        if ($SourceRevisionId) { $taskPublishProperties += "-p:SourceRevisionId=$SourceRevisionId" }
        & $taskDotnetPath publish src/Silen.App/Silen.App.csproj -c Release -r win-x64 --self-contained true @taskPublishProperties -o $taskStaging
        if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }
        $taskItems = @(Get-ChildItem -LiteralPath $taskStaging -Force)
        if ($taskItems.Count -ne 2 -or -not (Test-Path -LiteralPath (Join-Path $taskStaging 'Silen.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $taskStaging 'Archivos') -PathType Container)) {
            throw "La publicación no produjo la estructura esperada. Revisa $taskStaging."
        }

        $taskOutput = [System.IO.Path]::GetFullPath($taskOutput)
        if ((Split-Path -Parent $taskOutput) -ne $taskArtifacts -or (Split-Path -Leaf $taskOutput) -ne 'Silen-win-x64') { throw 'La salida está fuera del directorio de artifacts esperado.' }
        if (Test-Path -LiteralPath $taskOutput) {
            if ((Get-Item -LiteralPath $taskOutput -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'La salida no puede ser un enlace.' }
            Remove-Item -LiteralPath $taskOutput -Recurse -Force
        }
        Move-Item -LiteralPath $taskStaging -Destination $taskOutput
        $taskZip = Join-Path $taskRoot 'artifacts\Silen-win-x64.zip'
        Compress-Archive -LiteralPath $taskOutput -DestinationPath $taskZip -Force
        $taskManifest = Join-Path $taskRoot 'artifacts\Silen-release.json'
        if (Test-Path -LiteralPath $taskManifest) { Remove-Item -LiteralPath $taskManifest -Force }
        if ($SignRelease) { & (Join-Path $PSScriptRoot 'sign-release.ps1') -Action sign -Tag $ReleaseTag -Archive $taskZip -Manifest $taskManifest }
        else { Write-Output 'ZIP local sin firma de editor: el actualizador automático lo rechazará.' }
        Get-FileHash -LiteralPath $taskZip -Algorithm SHA256 | Format-List
        Write-Output "Aplicación: $(Join-Path $taskOutput 'Silen.exe')"
        Write-Output "Portable: $taskZip"
    }
}
finally { Pop-Location }
