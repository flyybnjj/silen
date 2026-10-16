param(
    [Parameter(Mandatory)][ValidateSet('init', 'sign', 'verify')][string]$Action,
    [string]$Tag,
    [string]$Archive,
    [string]$Manifest,
    [string]$KeyFile
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = Join-Path $env:USERPROFILE '.codex\runtimes\dotnet-10-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not $Archive) { $Archive = Join-Path $taskRoot 'artifacts\Silen-win-x64.zip' }
if (-not $Manifest) { $Manifest = Join-Path $taskRoot 'artifacts\Silen-release.json' }
if (-not $KeyFile) { $KeyFile = Join-Path $env:LOCALAPPDATA 'Silen\Signing\release-key.dpapi' }
if ($Action -ne 'init' -and -not $Tag) { throw 'Indica -Tag con la versión estable de la release.' }
& $taskDotnet run --project (Join-Path $PSScriptRoot 'signing\SigningTool.csproj') -c Release -- $Action $taskRoot $Tag $Archive $Manifest $KeyFile
if ($LASTEXITCODE -ne 0) { throw 'La firma/verificación no se completó; la actualización permanece bloqueada.' }
