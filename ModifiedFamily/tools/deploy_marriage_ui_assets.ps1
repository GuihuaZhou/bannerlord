[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$GameModulePath =
        'D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedFamily'
)

$ErrorActionPreference = 'Stop'
$devModulePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $devModulePath 'ModifiedFamily\ModifiedFamily.csproj'
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Development module validation failed: $devModulePath"
}

if (-not (Test-Path -LiteralPath $GameModulePath -PathType Container)) {
    throw "Game module directory does not exist: $GameModulePath"
}

$targetModuleFile = Join-Path $GameModulePath 'SubModule.xml'
if ((Split-Path -Leaf $GameModulePath) -ne 'ModifiedFamily' -or
    -not (Test-Path -LiteralPath $targetModuleFile -PathType Leaf)) {
    throw "Target is not a ModifiedFamily module directory: $GameModulePath"
}

$relativeFiles = @(
    'GUI\Prefabs\ModifiedFamilyMarriageShell.xml',
    'GUI\ModifiedFamilySpriteData.xml'
)
$assetGroups = @(
    @{ Directory = 'GUI\SpriteParts\ui_kingdomnegotiation'; Filter = '*.png' },
    @{ Directory = 'AssetSources\GauntletUI'; Filter = 'ui_kingdomnegotiation*.png' },
    @{ Directory = 'Assets\GauntletUI'; Filter = 'ui_kingdomnegotiation*.tpac' },
    @{ Directory = 'RuntimeDataCache'; Filter = '*.rdc' }
)

foreach ($assetGroup in $assetGroups) {
    $sourceDirectory = Join-Path $devModulePath $assetGroup.Directory
    $assets = @(Get-ChildItem -LiteralPath $sourceDirectory `
        -Filter $assetGroup.Filter -File)
    if ($assets.Count -eq 0) {
        throw "No assets matched $($assetGroup.Filter) in $sourceDirectory"
    }
    foreach ($asset in $assets) {
        $relativeFiles += $asset.FullName.Substring($devModulePath.Length + 1)
    }
}

foreach ($relativePath in $relativeFiles) {
    $sourcePath = Join-Path $devModulePath $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required marriage UI asset does not exist: $sourcePath"
    }

    $destinationPath = Join-Path $GameModulePath $relativePath
    if ($PSCmdlet.ShouldProcess($destinationPath, "Copy $sourcePath")) {
        New-Item -ItemType Directory -Force `
            -Path (Split-Path -Parent $destinationPath) | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
        $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
        $destinationHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destinationPath).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "SHA-256 verification failed: $relativePath"
        }
        Write-Host "Synchronized: $relativePath [$sourceHash]"
    }
}
