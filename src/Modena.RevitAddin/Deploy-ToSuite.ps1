<#
.SYNOPSIS
    Post-build deployment script for Modena Suite modules.

.DESCRIPTION
    Copies a module's build output to ModenaSuite\Modules\{AppId}\ for every
    Revit version installed on this machine (2024, 2025, 2026).

    Drop this file into the root of the module project, then add this Target
    to that project's .csproj (update AppId to match):

        <Target Name="DeployToModenaSuite" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug'">
            <Exec Command="powershell -ExecutionPolicy Bypass -NonInteractive -File &quot;$(ProjectDir)Deploy-ToSuite.ps1&quot; -AppId &quot;ModelInfoUpdater&quot; -SourceDir &quot;$(TargetDir)&quot;" />
        </Target>

.PARAMETER AppId
    The module ID as declared in the suite catalog (e.g. ModelInfoUpdater).
    Must match the Catalog/modules/{AppId}.json descriptor.

.PARAMETER SourceDir
    The build output directory. Pass $(TargetDir) from MSBuild.

.PARAMETER RevitVersions
    Revit versions to target. Defaults to 2024, 2025, 2026.
    Versions where the Revit Addins folder does not exist are silently skipped.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $AppId,

    [Parameter(Mandatory)]
    [string] $SourceDir,

    [string[]] $RevitVersions = @("2024", "2025", "2026")
)

$ErrorActionPreference = "Stop"

$appData  = [System.Environment]::GetFolderPath("ApplicationData")
$deployed = 0

Write-Host ""
Write-Host "------------------------------------------------------------"
Write-Host "  Modena Suite -- Deploy Module"
Write-Host "  App  : $AppId"
Write-Host "  From : $SourceDir"
Write-Host "------------------------------------------------------------"

foreach ($version in $RevitVersions) {

    $addinsRoot = Join-Path $appData "Autodesk\Revit\Addins\$version"

    if (-not (Test-Path -LiteralPath $addinsRoot)) {
        Write-Host "  [SKIP]  Revit $version -- addins folder not found."
        continue
    }

    $targetDir = Join-Path $addinsRoot "ModenaSuite\Modules\$AppId"
    Write-Host "  [COPY]  Revit $version --> $targetDir"

    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    $sourceNorm = $SourceDir.TrimEnd([char]'\', [char]'/')
    $items      = Get-ChildItem -LiteralPath $SourceDir -Recurse
    $fileCount  = 0

    foreach ($item in $items) {

        $relative = $item.FullName.Substring($sourceNorm.Length).TrimStart([char]'\', [char]'/')
        $dest     = Join-Path $targetDir $relative

        if ($item.PSIsContainer) {
            if (-not (Test-Path -LiteralPath $dest)) {
                New-Item -ItemType Directory -Path $dest -Force | Out-Null
            }
        }
        else {
            $destParent = Split-Path $dest -Parent
            if (-not (Test-Path -LiteralPath $destParent)) {
                New-Item -ItemType Directory -Path $destParent -Force | Out-Null
            }
            Copy-Item -LiteralPath $item.FullName -Destination $dest -Force
            $fileCount++
        }
    }

    Write-Host "         $fileCount file(s) copied."
    $deployed++
}

Write-Host "------------------------------------------------------------"

if ($deployed -eq 0) {
    Write-Warning "No matching Revit installations found -- nothing was deployed."
}
else {
    Write-Host "  Done. Deployed to $deployed Revit version(s)."
}

Write-Host ""
