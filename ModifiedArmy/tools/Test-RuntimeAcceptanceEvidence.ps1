param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot "..\baseline\T01_T24_ResultMatrix.md"),
    [switch]$RequireGo
)

$ErrorActionPreference = "Stop"
$rows = [System.Collections.Generic.List[object]]::new()
$invalid = [System.Collections.Generic.List[string]]::new()
$allowedStatuses = @("PASS", "FAIL", "BLOCKED", "WAIVED")

foreach ($line in Get-Content -LiteralPath $MatrixPath) {
    if ($line -notmatch '^\|\s*(T\d{2})\s*\|') { continue }

    $cells = @($line.Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 6) {
        $invalid.Add("$($matches[1]): expected 6 columns, found $($cells.Count)")
        continue
    }

    $row = [pscustomobject]@{
        Id = $cells[0]
        Scenario = $cells[1]
        Status = $cells[2].ToUpperInvariant()
        Evidence = $cells[3]
        Tester = $cells[4]
        Commit = $cells[5]
    }
    $rows.Add($row)
}

$expectedIds = 1..24 | ForEach-Object { "T{0:D2}" -f $_ }
foreach ($id in $expectedIds) {
    $matchesForId = @($rows | Where-Object Id -eq $id)
    if ($matchesForId.Count -ne 1) {
        $invalid.Add("${id}: expected exactly one row, found $($matchesForId.Count)")
    }
}

foreach ($row in $rows) {
    if ($row.Status -notin $allowedStatuses) {
        $invalid.Add("$($row.Id): unknown status '$($row.Status)'")
        continue
    }

    if ($row.Status -eq "PASS") {
        $missingEvidence = [string]::IsNullOrWhiteSpace($row.Evidence) -or
            $row.Evidence -match '^(No |Short smoke|User environment required|N/A)'
        $missingTester = [string]::IsNullOrWhiteSpace($row.Tester) -or
            $row.Tester -eq "User environment required"
        $missingCommit = [string]::IsNullOrWhiteSpace($row.Commit) -or
            $row.Commit -eq "remediation range"
        if ($missingEvidence -or $missingTester -or $missingCommit) {
            $invalid.Add("$($row.Id): PASS requires concrete evidence, tester and commit")
        }
    }

    if ($row.Status -eq "WAIVED" -and $row.Evidence -notmatch '(?i)waive|豁免|理由') {
        $invalid.Add("$($row.Id): WAIVED requires a recorded rationale")
    }
}

$groups = $rows | Group-Object Status | Sort-Object Name
foreach ($group in $groups) {
    Write-Host ("[{0}] {1}" -f $group.Name, $group.Count)
}

if ($invalid.Count -gt 0) {
    foreach ($problem in $invalid) { Write-Host "[INVALID] $problem" }
    Write-Error "Runtime acceptance evidence is malformed."
    exit 1
}

$criticalNotPassed = @($rows | Where-Object {
    [int]$_.Id.Substring(1) -le 20 -and $_.Status -ne "PASS"
})
$extendedOpen = @($rows | Where-Object {
    [int]$_.Id.Substring(1) -ge 21 -and $_.Status -notin @("PASS", "WAIVED")
})
$go = $criticalNotPassed.Count -eq 0 -and $extendedOpen.Count -eq 0

Write-Host ("Runtime acceptance decision: {0}" -f $(if ($go) { "GO" } else { "NO-GO" }))
if (-not $go) {
    Write-Host ("Open critical: {0}" -f (($criticalNotPassed.Id) -join ", "))
    Write-Host ("Open extended: {0}" -f (($extendedOpen.Id) -join ", "))
}

if ($RequireGo -and -not $go) { exit 2 }

