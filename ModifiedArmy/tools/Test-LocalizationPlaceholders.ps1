param(
    [string]$BasePath = "$PSScriptRoot/../ModifiedArmy/ModuleData/submodule_strings.xml",
    [string]$ChinesePath = "$PSScriptRoot/../ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml"
)

$ErrorActionPreference = 'Stop'

# The base XML id is not the localization id. The leading {=KEY} token maps a
# base entry to its optional language override, so it must be removed before
# comparing runtime placeholders.
[xml]$baseDocument = Get-Content -LiteralPath $BasePath -Raw
[xml]$chineseDocument = Get-Content -LiteralPath $ChinesePath -Raw
$baseByLocalizationKey = @{}
foreach ($entry in $baseDocument.strings.string) {
    if ($entry.text -match '^\{=([^}]+)\}') {
        $baseByLocalizationKey[$Matches[1]] = [string]$entry.text
    }
}

function Get-Placeholders([string]$text) {
    # Runtime variables use uppercase names. This deliberately ignores the
    # mixed-case localization key consumed by TaleWorlds at the start of text.
    return @([regex]::Matches($text, '\{([A-Z][A-Z0-9_]*)\}') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique)
}

$failures = @()
foreach ($entry in $chineseDocument.strings.string) {
    if (!$baseByLocalizationKey.ContainsKey($entry.id)) {
        continue
    }
    $baseVariables = Get-Placeholders $baseByLocalizationKey[$entry.id]
    $translatedVariables = Get-Placeholders ([string]$entry.text)
    $difference = @(Compare-Object $baseVariables $translatedVariables)
    if ($difference.Count -gt 0) {
        $failures += "{0}: base=[{1}] zh-CN=[{2}]" -f `
            $entry.id, ($baseVariables -join ','), ($translatedVariables -join ',')
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Localization placeholder validation passed."
