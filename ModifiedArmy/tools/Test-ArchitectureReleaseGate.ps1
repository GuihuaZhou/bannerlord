param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$ProtectionBase = "72ef039"
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Check {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $state = if ($Passed) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1}: {2}" -f $state, $Name, $Detail)
    if (-not $Passed) { $failures.Add($Name) }
}

function Read-Text([string]$RelativePath) {
    return Get-Content -LiteralPath (Join-Path $RepositoryRoot $RelativePath) -Raw
}

Push-Location $RepositoryRoot
try {
    dotnet build .\ModifiedArmy\ModifiedArmy.sln -c Release --nologo --verbosity quiet
    Add-Check "ModifiedArmy build" ($LASTEXITCODE -eq 0) "Release build"

    dotnet build .\ModifiedPolitics\ModifiedPolitics.sln -c Release --nologo --verbosity quiet
    Add-Check "ModifiedPolitics build" ($LASTEXITCODE -eq 0) "Release build"

    $armyProject = Read-Text "ModifiedArmy\ModifiedArmy\ModifiedArmy.csproj"
    $armyManifest = Read-Text "ModifiedArmy\ModifiedArmy\SubModule.xml"
    $politicsManifest = Read-Text "ModifiedPolitics\SubModule.xml"
    $noArmyToPolitics =
        $armyProject -notmatch 'Reference Include="ModifiedPolitics"' -and
        $armyManifest -notmatch 'DependedModule Id="ModifiedPolitics"'
    Add-Check "No module dependency cycle" $noArmyToPolitics `
        "Army has no compile/manifest dependency on Politics"
    Add-Check "Politics integration direction" `
        ($politicsManifest -match 'DependedModule Id="ModifiedArmy"') `
        "Politics remains the integration owner"

    $fiefManager = Read-Text `
        "ModifiedArmy\ModifiedArmy\Fief\Models\FiefPartyManager.cs"
    Add-Check "Legacy reinforcement SaveId" `
        ($fiefManager -match 'ReinforcementMission\), 8\)') "SaveId 8"
    Add-Check "Transport component SaveId" `
        ($fiefManager -match 'ReinforcementPartyComponent\), 9\)') "SaveId 9"

    $protected = git diff --name-only $ProtectionBase -- `
        ModifiedArmy/ARCHITECTURE.md `
        ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1
    Add-Check "Protected files" ([string]::IsNullOrWhiteSpace($protected)) `
        "No changes since $ProtectionBase"

    $retired = git grep -n -E `
        'ReinforcementMissionService|IArmyMissionProtectionModel|ModifiedArmy\.PartyFinance' `
        -- ModifiedArmy/ModifiedArmy 2>$null
    Add-Check "Retired architecture references" `
        ([string]::IsNullOrWhiteSpace($retired)) "No active references"

    $supplyBehavior = Read-Text `
        "ModifiedArmy\ModifiedArmy\Garrison\Supply\SupplyPartyBehavior.cs"
    Add-Check "Supply behavior roster writes" `
        ($supplyBehavior -notmatch 'AddToCounts\s*\(') `
        "Roster mutations remain in SupplyCargoService"

    $taskBehavior = Read-Text `
        "ModifiedArmy\ModifiedArmy\ForceManagement\Behaviors\AiForcePlanningBehavior.cs"
    $legacyHelpers = @(
        "CaptureTaskSaveFields", "RestoreTaskSaveFields", "TryParseRecord",
        "TryRoutineDonation", "ExecuteWarRest", "TryPeaceDemobilization"
    ) | Where-Object { $taskBehavior.Contains($_) }
    Add-Check "AI behavior duplicate helpers" ($legacyHelpers.Count -eq 0) `
        "Persistence/coordinator/visit services are authoritative"

    $garrisonBehavior = Read-Text `
        "ModifiedArmy\ModifiedArmy\Recruitment\Behaviors\GarrisonRecruitFromPrisonersBehavior.cs"
    $garrisonWrites = $garrisonBehavior -match `
        'AutoRecruitmentExpenses|CommitRecruitment\s*\('
    Add-Check "Garrison behavior finance writes" (-not $garrisonWrites) `
        "Execution service owns expense and budget commits"

    if ($failures.Count -gt 0) {
        Write-Error ("Architecture release gate failed: " +
            ($failures -join ", "))
        exit 1
    }
    Write-Host "Architecture release gate PASS. Game scenarios T01-T24 remain separate."
}
finally {
    Pop-Location
}
