$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExe = Join-Path $taskRoot 'artifacts\Silen-win-x64\Silen.exe'
if (Test-Path -LiteralPath $taskExe) {
    Start-Process -FilePath $taskExe
    return
}
$taskBundled = Join-Path $env:USERPROFILE '.codex\runtimes\dotnet-10-sdk\dotnet.exe'
$taskDotnet = if (Test-Path -LiteralPath $taskBundled) { $taskBundled } else { 'dotnet' }
Push-Location $taskRoot
try { & $taskDotnet run --project src/Silen.App/Silen.App.csproj }
finally { Pop-Location }
