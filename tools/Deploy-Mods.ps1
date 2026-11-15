<#
.SYNOPSIS
Builds optionally, then deploys all selected repository mods.

.DESCRIPTION
With -Build, compiled mods are built first without restoring packages. Their
outputs are deployed from bin/<Configuration>/netstandard2.0. SubModule.xml is
copied from source. ModifiedTroops is an XML-only special case and is deployed
directly from its source directory.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Build,

    [string[]]$Modules = @(
        "ModifiedArmy",
        "ModifiedPolitics",
        "ModifiedDiplomacy",
        "ModifiedFamily",
        "ModifiedTroops"
    ),

    [string]$GameRoot =
        "D:\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeModulesRoot = Join-Path $GameRoot "Modules"

$specifications = @{
    ModifiedArmy = @{
        Manifest = "ModifiedArmy\ModifiedArmy\SubModule.xml"
        Output = "ModifiedArmy\ModifiedArmy\bin"
        Project = "ModifiedArmy\ModifiedArmy\ModifiedArmy.csproj"
    }
    ModifiedPolitics = @{
        Manifest = "ModifiedPolitics\SubModule.xml"
        Output = "ModifiedPolitics\ModifiedPolitics\bin"
        Project = "ModifiedPolitics\ModifiedPolitics\ModifiedPolitics.csproj"
    }
    ModifiedDiplomacy = @{
        Manifest = "ModifiedDiplomacy\SubModule.xml"
        Output = "ModifiedDiplomacy\ModifiedDiplomacy\bin"
        Project = "ModifiedDiplomacy\ModifiedDiplomacy\ModifiedDiplomacy.csproj"
    }
    ModifiedFamily = @{
        Manifest = "ModifiedFamily\SubModule.xml"
        Output = "ModifiedFamily\ModifiedFamily\bin"
        Project = "ModifiedFamily\ModifiedFamily\ModifiedFamily.csproj"
    }
    ModifiedTroops = @{
        Manifest = "ModifiedTroops\SubModule.xml"
        SourceOnly = "ModifiedTroops"
    }
}

if ($Build) {
    foreach ($moduleName in $Modules) {
        if (-not $specifications.ContainsKey($moduleName)) {
            throw "Unknown module '$moduleName'. Supported modules: $($specifications.Keys -join ', ')"
        }
        $projectRelativePath = $specifications[$moduleName].Project
        if (-not $projectRelativePath) {
            Write-Host "[SKIP] $moduleName has no compiled project."
            continue
        }
        $projectPath = Join-Path $repositoryRoot $projectRelativePath
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "Project not found for ${moduleName}: $projectPath"
        }
        Write-Host "[BUILD] $moduleName ($Configuration)"
        & dotnet build $projectPath -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $moduleName with exit code $LASTEXITCODE"
        }
    }
}

function Copy-VerifiedFile {
    param(
        [Parameter(Mandatory)] [string]$Source,
        [Parameter(Mandatory)] [string]$Target,
        [Parameter(Mandatory)] $Caller
    )

    if (-not $Caller.ShouldProcess($Target, "Copy $Source")) { return $false }
    $targetParent = Split-Path -Parent $Target
    if (-not (Test-Path -LiteralPath $targetParent -PathType Container)) {
        New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
    }
    Copy-Item -LiteralPath $Source -Destination $Target -Force
    $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) {
        throw "Deployment verification failed: $Target"
    }
    return $true
}

foreach ($moduleName in $Modules) {
    if (-not $specifications.ContainsKey($moduleName)) {
        throw "Unknown module '$moduleName'. Supported modules: $($specifications.Keys -join ', ')"
    }

    $specification = $specifications[$moduleName]
    $sourceManifest = Join-Path $repositoryRoot $specification.Manifest
    if (-not (Test-Path -LiteralPath $sourceManifest -PathType Leaf)) {
        throw "Source manifest not found: $sourceManifest"
    }
    $manifest = [xml](Get-Content -LiteralPath $sourceManifest -Raw)
    $moduleId = $manifest.Module.Id.value
    if ([string]::IsNullOrWhiteSpace($moduleId)) {
        throw "Module ID is missing: $sourceManifest"
    }

    $targetModule = Join-Path $runtimeModulesRoot $moduleId
    $targetManifest = Join-Path $targetModule "SubModule.xml"
    if (-not (Test-Path -LiteralPath $targetManifest -PathType Leaf)) {
        throw "Runtime module is not installed: $targetModule"
    }
    $targetManifestXml = [xml](Get-Content -LiteralPath $targetManifest -Raw)
    if ($targetManifestXml.Module.Id.value -ne $moduleId) {
        throw "Runtime module ID mismatch: $targetManifest"
    }

    $copied = 0
    if (Copy-VerifiedFile -Source $sourceManifest -Target $targetManifest `
            -Caller $PSCmdlet) {
        $copied++
    }

    if ($specification.SourceOnly) {
        $sourceRoot = Join-Path $repositoryRoot $specification.SourceOnly
        $sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
            Where-Object { $_.FullName -ne $sourceManifest })
        foreach ($sourceFile in $sourceFiles) {
            $relative = $sourceFile.FullName.Substring(
                $sourceRoot.Length).TrimStart('\')
            $target = Join-Path $targetModule $relative
            if (Copy-VerifiedFile -Source $sourceFile.FullName -Target $target `
                    -Caller $PSCmdlet) {
                $copied++
            }
        }
    }
    else {
        $outputRoot = Join-Path (Join-Path $repositoryRoot `
            $specification.Output) "$Configuration\netstandard2.0"
        if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
            throw "Build output not found for ${moduleName}: $outputRoot"
        }

        $targetBinary = Join-Path $targetModule "bin\Win64_Shipping_Client"
        if (-not (Test-Path -LiteralPath $targetBinary -PathType Container)) {
            throw "Runtime binary directory not found: $targetBinary"
        }

        # Only the module's own top-level assembly artifacts are deployable.
        # CopyLocal game/framework dependencies must not shadow Bannerlord DLLs.
        $binaryFiles = @(Get-ChildItem -LiteralPath $outputRoot -File |
            Where-Object { $_.Name -like "$moduleId.*" })
        if (@($binaryFiles | Where-Object { $_.Name -eq "$moduleId.dll" }).Count -ne 1) {
            throw "Compiled module DLL not found: $outputRoot\$moduleId.dll"
        }
        foreach ($sourceFile in $binaryFiles) {
            $target = Join-Path $targetBinary $sourceFile.Name
            if (Copy-VerifiedFile -Source $sourceFile.FullName -Target $target `
                    -Caller $PSCmdlet) {
                $copied++
            }
        }

        foreach ($sourceDirectory in Get-ChildItem -LiteralPath $outputRoot -Directory) {
            $directoryTargetRoot = if ($sourceDirectory.Name -in @(
                    "GUI", "ModuleData")) {
                $targetModule
            }
            else {
                $targetBinary
            }
            foreach ($sourceFile in Get-ChildItem -LiteralPath `
                $sourceDirectory.FullName -File -Recurse) {
                $relative = $sourceFile.FullName.Substring(
                    $outputRoot.Length).TrimStart('\')
                $target = Join-Path $directoryTargetRoot $relative
                if (Copy-VerifiedFile -Source $sourceFile.FullName `
                        -Target $target -Caller $PSCmdlet) {
                    $copied++
                }
            }
        }
    }

    Write-Host ("[PASS] {0} ({1}) deployed files: {2}" -f `
        $moduleName, $moduleId, $copied)
}

Write-Host ($(if ($Build) {
    "All selected mods built and deployed."
} else {
    "All selected mods deployed. No build was performed."
}))
