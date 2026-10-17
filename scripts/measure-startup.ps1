param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,10)][int]$Repetitions = 3
)
$ErrorActionPreference = 'Stop'
$startupExe = (Resolve-Path -LiteralPath $Executable).Path
$startupOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $startupOutput) { throw 'Usa una carpeta nueva para conservar las mediciones anteriores.' }
New-Item -ItemType Directory -Path $startupOutput | Out-Null
Get-FileHash -LiteralPath $startupExe -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $startupOutput 'binary.json') -Encoding utf8
$startupRows = @()
foreach ($startupTrial in 1..$Repetitions) {
    $startupDirectory = Join-Path $startupOutput "trial-$startupTrial"
    $startupInfo = [Diagnostics.ProcessStartInfo]::new($startupExe)
    $startupInfo.UseShellExecute = $false
    $startupInfo.Environment['SILEN_STARTUP_DIAGNOSTICS_DIRECTORY'] = $startupDirectory
    $startupClock = [Diagnostics.Stopwatch]::StartNew()
    $startupProcess = [Diagnostics.Process]::Start($startupInfo)
    try {
        if (-not $startupProcess.WaitForExit(60000)) { throw 'El arranque superó los 60 s; la toma se conserva sin contarla.' }
        if ($startupProcess.ExitCode -ne 0) { throw 'La instancia de medición del arranque terminó con error.' }
        $startupResult = Get-Content -LiteralPath (Join-Path $startupDirectory 'startup.json') -Raw | ConvertFrom-Json
        if (-not $startupResult.ProductionEntryPoint -or -not $startupResult.Success -or $startupResult.Samples -ne 10) { throw 'La toma no acredita arranque real y diagnóstico completo de diez muestras.' }
        $startupRows += [pscustomobject]@{ Trial=$startupTrial; ExternalUntilExitSeconds=$startupClock.Elapsed.TotalSeconds; Result=$startupResult }
        $startupRows | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $startupOutput 'measurements.json') -Encoding utf8
        Write-Output "Arranque real $startupTrial`: básicos=$([Math]::Round($startupResult.Milestones.BasicsVisibleLayout/1000,3)) s; inicialización completa=$([Math]::Round($startupResult.Milestones.AllStartupWork/1000,3)) s"
    }
    finally {
        if (-not $startupProcess.HasExited) { $startupProcess.Kill(); $startupProcess.WaitForExit() }
        $startupProcess.Dispose()
    }
}
$startupSummary = @{}
foreach ($startupMark in @('WindowShown','Identity','RamCapacity','Drives','FirstInformationVisibleLayout','BasicsVisibleLayout','FirstSample','TenSamples','CompleteSnapshot','AllStartupWork')) {
    if (@($startupRows | Where-Object { $null -eq $_.Result.Milestones.$startupMark }).Count -gt 0) { continue }
    $startupValues = @($startupRows | ForEach-Object { $_.Result.Milestones.$startupMark / 1000 } | Sort-Object)
    $startupSummary[$startupMark] = @{ Median=$startupValues[[int][Math]::Floor($startupValues.Count/2)]; Min=$startupValues[0]; Max=$startupValues[-1] }
}
$startupSummary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $startupOutput 'summary.json') -Encoding utf8
