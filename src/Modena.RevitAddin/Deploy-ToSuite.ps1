# Explicit deployment only: one build may target only its own Revit year.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('ModelHealthChecker')][string]$AppId,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][ValidateSet('2024','2025','2026','2027')][string]$RevitYear
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../../scripts/RevitArtifacts.ps1"
$artifact = $RevitArtifacts | Where-Object Year -eq $RevitYear
Assert-RevitPayload $SourceDir $artifact
$target = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Autodesk/Revit/Addins/$RevitYear/ModenaSuite/Modules/$AppId"
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path "$SourceDir/*" -Destination $target -Recurse -Force
$registry = Join-Path $target 'Registry/ModelHealthChecker.json'
$descriptor = Get-Content $registry -Raw | ConvertFrom-Json
$descriptor.assemblyPath = Join-Path $target 'Modena.RevitAddin.dll'
$descriptor | ConvertTo-Json -Depth 10 | Set-Content $registry -Encoding UTF8
Write-Host "Deployed exact Revit $RevitYear build to $target"
