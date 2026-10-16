param([switch]$RequireSignedRelease, [string]$ReleaseTag, [ValidateRange(60, 180)][int]$TimeoutSeconds = 60)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPackage = Join-Path $taskRoot 'artifacts\Silen-win-x64'
$taskZip = Join-Path $taskRoot 'artifacts\Silen-win-x64.zip'
$taskManifest = Join-Path $taskRoot 'artifacts\Silen-release.json'
if ($RequireSignedRelease -or (Test-Path -LiteralPath $taskManifest)) {
    if (-not $ReleaseTag) { throw 'La verificación de firma requiere -ReleaseTag con la versión estable esperada.' }
    & (Join-Path $PSScriptRoot 'sign-release.ps1') -Action verify -Tag $ReleaseTag -Archive $taskZip -Manifest $taskManifest
}
$taskItems = @(Get-ChildItem -LiteralPath $taskPackage -Force)
if ($taskItems.Count -ne 2 -or -not (Test-Path -LiteralPath (Join-Path $taskPackage 'Silen.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $taskPackage 'Archivos') -PathType Container)) {
    throw 'La carpeta portable debe contener solamente Silen.exe y Archivos.'
}
foreach ($taskRequired in @('README.md', 'THIRD-PARTY-NOTICES.md', 'licenses\WinUtil-MIT.txt', 'tools\WinUtil\fortnite-winutil.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskPackage "Archivos\$taskRequired") -PathType Leaf)) { throw "Falta el archivo de soporte: $taskRequired" }
}
$taskPinned = Join-Path $taskPackage 'Archivos\tools\WinUtil\fortnite-winutil.json'
if ((Get-FileHash -LiteralPath $taskPinned -Algorithm SHA256).Hash -ne '354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B') { throw 'El recurso fijado de WinUtil fue alterado.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive = [System.IO.Compression.ZipFile]::OpenRead($taskZip)
try {
    $taskFiles = @(Get-ChildItem -LiteralPath $taskPackage -Recurse -File)
    $taskZipFiles = @($taskArchive.Entries | Where-Object { $_.Name })
    if ($taskFiles.Count -ne $taskZipFiles.Count) { throw 'El ZIP no coincide con los archivos publicados.' }
    $taskNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($taskEntry in $taskArchive.Entries) {
        $taskName = $taskEntry.FullName.Replace('\', '/')
        if (-not $taskName.StartsWith('Silen-win-x64/') -or $taskName.Contains('../') -or $taskName.Contains(':') -or -not $taskNames.Add($taskName)) { throw "Ruta inesperada o repetida en el ZIP: $taskName" }
        if (-not $taskEntry.Name) { continue }
        $taskRelative = $taskName.Substring('Silen-win-x64/'.Length)
        $taskFile = Join-Path $taskPackage $taskRelative
        if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw "Archivo inesperado en el ZIP: $taskRelative" }
        $taskStream = $taskEntry.Open()
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            $taskHash = ([System.BitConverter]::ToString($sha.ComputeHash($taskStream)) -replace '-').ToUpperInvariant()
        }
        finally { $taskStream.Dispose() }
        if ($taskHash -ne (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash) { throw "El ZIP contiene una versión diferente de $taskRelative" }
    }
}
finally { $taskArchive.Dispose() }


$taskEvidence = Join-Path $taskRoot ('artifacts\portable verification ' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($taskEvidence) | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($taskZip, $taskEvidence)
$taskExtracted = Join-Path $taskEvidence 'Silen-win-x64'
$taskSmoke = Join-Path $taskEvidence 'smoke'
$taskStart = [System.Diagnostics.ProcessStartInfo]::new()
$taskStart.FileName = Join-Path $taskExtracted 'Silen.exe'
$taskStart.WorkingDirectory = $env:TEMP
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$taskStart.RedirectStandardOutput = $true
$taskStart.RedirectStandardError = $true
$taskStart.Arguments = "--smoke-test `"$taskSmoke`""
$taskStart.EnvironmentVariables['PATH'] = Join-Path $env:WINDIR 'System32'
$taskStart.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
foreach ($taskVariable in @('DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT(x86)', 'MSBuildSDKsPath')) {
    if ($taskStart.EnvironmentVariables.ContainsKey($taskVariable)) { $taskStart.EnvironmentVariables.Remove($taskVariable) }
}
$taskProcess = [System.Diagnostics.Process]::Start($taskStart)
try {
    $taskStdout = $taskProcess.StandardOutput.ReadToEndAsync()
    $taskStderr = $taskProcess.StandardError.ReadToEndAsync()
    if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) { $taskProcess.Kill($true); throw "La prueba portable excedió $TimeoutSeconds segundos." }
    $taskStdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $taskEvidence 'stdout.txt')
    $taskStderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $taskEvidence 'stderr.txt')
    if ($taskProcess.ExitCode -ne 0) { throw "El ZIP extraído falló con código $($taskProcess.ExitCode). Evidencia: $taskEvidence" }
}
finally { $taskProcess.Dispose() }
$taskStatus = Get-Content -LiteralPath (Join-Path $taskSmoke 'status.json') -Raw | ConvertFrom-Json
$taskSnapshot = Get-Content -LiteralPath (Join-Path $taskSmoke 'snapshot.json') -Raw | ConvertFrom-Json
if (@($taskSnapshot.Startup | Where-Object { $_.Command -and $_.Command -ne '[comando local omitido]' }).Count -gt 0) { throw 'El diagnóstico exportado todavía incluye comandos de inicio.' }
if (-not $taskStatus.Success -or $taskStatus.AppliedSystemChanges -or $taskStatus.CapturedGaming -or $taskStatus.InteractionChecks -lt 105 -or -not $taskStatus.UniversalOptimizationReady) { throw 'La prueba de la app portable no cumple los contratos esperados.' }
[pscustomobject]@{ Success = $true; RootItems = @($taskItems.Name); VerifiedArchiveFiles = $taskFiles.Count; InteractionChecks = $taskStatus.InteractionChecks; Evidence = $taskEvidence } | ConvertTo-Json -Depth 4
