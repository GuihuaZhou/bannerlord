param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot `
        "..\baseline\GarrisonRecruitmentOrders_T01_T28_ResultMatrix.md"),
    [switch]$RequireGo
)

$ErrorActionPreference = "Stop"
$rows = @()
foreach ($line in Get-Content -LiteralPath $MatrixPath) {
    if ($line -notmatch '^\|\s*(T\d{2})\s*\|') { continue }
    $cells = @($line.Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 6) { throw "$($matches[1]) has $($cells.Count) columns" }
    $rows += [pscustomobject]@{ Id=$cells[0]; Status=$cells[2].ToUpperInvariant() }
}

$expected = 1..28 | ForEach-Object { "T{0:D2}" -f $_ }
foreach ($id in $expected) {
    if (@($rows | Where-Object Id -eq $id).Count -ne 1) {
        throw "$id must appear exactly once"
    }
}
foreach ($row in $rows) {
    if ($row.Status -notin @("PASS", "FAIL", "BLOCKED", "WAIVED")) {
        throw "$($row.Id) has invalid status $($row.Status)"
    }
}

$open = @($rows | Where-Object Status -notin @("PASS", "WAIVED"))
Write-Host ("Garrison order evidence: {0} PASS/WAIVED, {1} open" -f `
    ($rows.Count - $open.Count), $open.Count)
if ($RequireGo -and $open.Count -gt 0) { exit 2 }
