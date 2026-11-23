param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputPath = 'publish',
    [string]$Version = '1.0.6'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/scripts/RevitArtifacts.ps1"
$repo = $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
if (-not $output.StartsWith($repo.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be a subdirectory of the repository.'
}
$staging = Join-Path $output 'Installer'
Reset-ArtifactDirectory $staging $output
foreach ($artifact in $RevitArtifacts) {
    $config = "$Configuration $($artifact.Configuration)"
    $project = Join-Path $repo $artifact.Project
    & dotnet build $project -c $config -p:Platform=x64 "-p:Version=$Version" "-p:AssemblyVersion=$Version.0" "-p:FileVersion=$Version.0" "-p:AssemblyInformationalVersion=$Version" -p:DeployToRevit=false
    if ($LASTEXITCODE -ne 0) { throw "Revit $($artifact.Year) build failed; no complete package is available." }
    $source = Join-Path (Split-Path $project) "bin/$config/$($artifact.Folder)"
    Assert-RevitPayload $source $artifact
    $destination = Join-Path $staging $artifact.Folder
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path "$source/*" -Destination $destination -Recurse -Force
    # Suite descriptor is specific to the same Revit year as the DLL.
    $registry = Join-Path $destination 'Registry/ModelHealthChecker.json'
    $descriptor = Get-Content $registry -Raw | ConvertFrom-Json
    $descriptor.assemblyPath = "%APPDATA%\Autodesk\Revit\Addins\$($artifact.Year)\ModenaSuite\Modules\ModelHealthChecker\Modena.RevitAddin.dll"
    $descriptor | ConvertTo-Json -Depth 10 | Set-Content $registry -Encoding UTF8
    Assert-RevitPayload $destination $artifact
}
Write-Host "All four exact Revit payloads staged in $staging"
