param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$taskPatterns = @(
    ('-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?' + 'PRIVATE KEY-----'),
    '\bAIza[0-9A-Za-z_-]{35}\b',
    '\bgh[pousr]_[0-9A-Za-z]{30,}\b',
    '\bgithub_pat_[0-9A-Za-z_]{50,}\b',
    '\bsk-(?:proj-|svcacct-)?[0-9A-Za-z_-]{32,}\b',
    '\bAKIA[0-9A-Z]{16}\b'
)
$taskFindings = [System.Collections.Generic.List[string]]::new()
Push-Location $Root
try {
    $taskFiles = @(& git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo obtener el inventario para revisar secretos.' }
    foreach ($taskFile in $taskFiles) {
        if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf)) { continue }
        if ($taskFile -notmatch '(?i)(\.(cs|csproj|props|targets|json|md|txt|yml|yaml|ps1|psm1|pem|key|config|xml|toml|ini|env|sh)|(^|/)\.env[^/]*)$') { continue }
        $taskText = [System.IO.File]::ReadAllText((Join-Path $Root $taskFile))
        foreach ($taskPattern in $taskPatterns) {
            if ([regex]::IsMatch($taskText, $taskPattern)) { $taskFindings.Add($taskFile); break }
        }
    }
}
finally { Pop-Location }
if ($taskFindings.Count -gt 0) {
    foreach ($taskFile in $taskFindings) { Write-Output "Posible secreto en: $taskFile" }
    throw "Se detectaron $($taskFindings.Count) archivos con posibles secretos. No se imprimieron valores."
}
Write-Output 'Revisión de patrones de secretos completada sin coincidencias. Alcance: archivos versionados y nuevos no ignorados; no sustituye una auditoría del historial.'
