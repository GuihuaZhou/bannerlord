param([switch]$RequireGo)

$ErrorActionPreference = "Stop"
$powerShell = (Get-Process -Id $PID).Path
$staticGate = Join-Path $PSScriptRoot "Test-ArchitectureReleaseGate.ps1"
$runtimeGate = Join-Path $PSScriptRoot "Test-RuntimeAcceptanceEvidence.ps1"

Write-Host "=== ModifiedArmy static release gate ==="
& $powerShell -NoProfile -File $staticGate
if ($LASTEXITCODE -ne 0) {
    Write-Error "Static release gate failed with exit code $LASTEXITCODE."
    exit 1
}

Write-Host "=== ModifiedArmy runtime evidence gate ==="
$runtimeArguments = @("-NoProfile", "-File", $runtimeGate)
if ($RequireGo) { $runtimeArguments += "-RequireGo" }
& $powerShell @runtimeArguments
$runtimeExit = $LASTEXITCODE

if ($runtimeExit -eq 0) {
    Write-Host "Combined release validation completed."
    exit 0
}
if ($runtimeExit -eq 2 -and $RequireGo) {
    Write-Host "Static checks passed, but runtime evidence does not permit GO."
    exit 2
}

Write-Error "Runtime evidence gate failed with exit code $runtimeExit."
exit 1

