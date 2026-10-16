param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$Repetitions = 3
)
$ErrorActionPreference = 'Stop'
$taskExe = (Resolve-Path -LiteralPath $Executable).Path
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $taskOutput) { throw 'Usa una carpeta nueva para conservar todas las tomas anteriores.' }
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskRuntime = Join-Path $env:USERPROFILE '.codex/runtimes/dotnet-10-sdk/shared/Microsoft.WindowsDesktop.App/10.0.13'
Add-Type -Path (Join-Path $taskRuntime 'UIAutomationTypes.dll')
Add-Type -Path (Join-Path $taskRuntime 'UIAutomationClient.dll')
$taskRows = @()
$taskAccepted = 0
for ($taskTrial = 1; $taskAccepted -lt $Repetitions -and $taskTrial -le ($Repetitions + 5); $taskTrial++) {
    $taskDir = Join-Path $taskOutput "trial-$taskTrial"
    New-Item -ItemType Directory -Path $taskDir | Out-Null
    $taskClock = [Diagnostics.Stopwatch]::StartNew()
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @('--performance-test', ('"' + $taskDir + '"')) -PassThru -WindowStyle Hidden
    try {
        $taskIdle = $taskProcess.WaitForInputIdle(15000)
        $taskIdleMs = $taskClock.Elapsed.TotalMilliseconds
        $taskRoot = $null; $taskWindowMs = $null; $taskAnalysisMs = $null; $taskErrors = 0
        $taskPidCondition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $taskProcess.Id)
        $taskTextCondition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Text)
        while (-not $taskProcess.HasExited -and $taskClock.Elapsed.TotalSeconds -lt 90 -and $null -eq $taskAnalysisMs) {
            try {
                if ($null -eq $taskRoot) { $taskRoot = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, $taskPidCondition) }
                if ($null -ne $taskRoot) {
                    if ($null -eq $taskWindowMs) { $taskWindowMs = $taskClock.Elapsed.TotalMilliseconds }
                    $taskTexts = $taskRoot.FindAll([Windows.Automation.TreeScope]::Descendants, $taskTextCondition)
                    foreach ($taskText in $taskTexts) {
                        if ($taskText.Current.Name -like 'Último análisis:*') { $taskAnalysisMs = $taskClock.Elapsed.TotalMilliseconds; break }
                    }
                }
            } catch { $taskErrors++ }
            if ($null -eq $taskAnalysisMs) { Start-Sleep -Milliseconds 100 }
            $taskProcess.Refresh()
        }
        if (-not $taskProcess.WaitForExit(120000)) { throw 'La toma superó el límite.' }
        $taskProbe = Get-Content -LiteralPath (Join-Path $taskDir 'performance.json') -Raw | ConvertFrom-Json
        $taskValid = $taskProcess.ExitCode -eq 0 -and $null -ne $taskAnalysisMs -and $taskProbe.Samples[0].ActiveForSample -and $taskProbe.Samples[0].RenderedFrames -gt 0
        $taskRow = [ordered]@{ Trial=$taskTrial; Included=$taskValid; ExitCode=$taskProcess.ExitCode; InputIdle=$taskIdle;
            StartupInputIdleMs=$taskIdleMs; FirstAutomationWindowMs=$taskWindowMs; FirstAnalysisVisibleMs=$taskAnalysisMs;
            AnalysisAfterWindowMs=($taskAnalysisMs-$taskWindowMs); AutomationReadErrors=$taskErrors; Probe=$taskProbe }
        $taskRow | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskDir 'observations.json') -Encoding utf8
        $taskRows += $taskRow
        $taskRows | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskOutput 'measurements.json') -Encoding utf8
        if ($taskValid) { $taskAccepted++ }
        Write-Output "Toma $taskTrial`: incluida=$taskValid; análisis=$([Math]::Round($taskAnalysisMs/1000,2)) s"
    } finally {
        if (-not $taskProcess.HasExited) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
        $taskProcess.Dispose()
    }
}
Get-FileHash -LiteralPath $taskExe -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'binary.json') -Encoding utf8
if ($taskAccepted -ne $Repetitions) { throw 'No se lograron tres tomas activas comparables; se conservan las tomas sin mezclar.' }
$taskMetrics = [ordered]@{}
$taskSelectors = [ordered]@{
    WindowSeconds = { param($r) $r.FirstAutomationWindowMs / 1000 }
    AnalysisAfterWindowSeconds = { param($r) $r.AnalysisAfterWindowMs / 1000 }
    TotalUntilAnalysisSeconds = { param($r) $r.FirstAnalysisVisibleMs / 1000 }
    WorkingSetMiB = { param($r) $r.Probe.Samples[0].WorkingSetMiB }
    PrivateCommitMiB = { param($r) $r.Probe.Samples[0].PrivateMiB }
    CpuPercentMachine = { param($r) $r.Probe.Samples[0].CpuPercentOfMachine }
}
foreach ($taskKey in $taskSelectors.Keys) {
    $taskValues = @($taskRows | Where-Object Included | ForEach-Object { & $taskSelectors[$taskKey] $_ } | Sort-Object)
    $taskMetrics[$taskKey] = @{ Median=$taskValues[[int][Math]::Floor($taskValues.Count/2)]; Min=$taskValues[0]; Max=$taskValues[-1] }
}
$taskMetrics | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput 'summary.json') -Encoding utf8
