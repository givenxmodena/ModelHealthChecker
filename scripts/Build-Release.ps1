param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [switch]$PublishToGitHub,
    [switch]$SkipVelopack
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/RevitArtifacts.ps1"
& "$repo/build-multi-version.ps1" -Version $Version
$output = Join-Path $repo 'build-output'
$package = Join-Path $output 'package'
Reset-ArtifactDirectory $package $output
& dotnet publish "$repo/src/Modena.RevitAddin.Updater/Modena.RevitAddin.Updater.csproj" -c Release -r win-x64 --self-contained false -o $package "-p:Version=$Version" "-p:AssemblyVersion=$Version.0" "-p:FileVersion=$Version.0" "-p:AssemblyInformationalVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Updater publish failed.' }
$releases = Join-Path $repo 'releases'
New-Item -ItemType Directory -Path $releases -Force | Out-Null
$manual = Join-Path $output 'manual'
Reset-ArtifactDirectory $manual $output
$archives = @()
foreach ($artifact in $RevitArtifacts) {
    $source = Join-Path $repo "publish/Installer/$($artifact.Folder)"
    Assert-RevitPayload $source $artifact
    Copy-Item -LiteralPath $source -Destination $package -Recurse -Force
    Assert-RevitPayload (Join-Path $package $artifact.Folder) $artifact
    $zipRoot = Join-Path $manual $artifact.Year
    $addinDir = Join-Path $zipRoot 'ModelHealthChecker'
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    Copy-Item -Path "$source/*" -Destination $addinDir -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $addinDir 'Modena.RevitAddin.addin')
    [xml]$manifest = Get-Content "$repo/src/Modena.RevitAddin/Modena.RevitAddin.addin"
    $manifest.RevitAddIns.AddIn.Assembly = 'ModelHealthChecker\Modena.RevitAddin.dll'
    $manifest.Save((Join-Path $zipRoot 'Modena.RevitAddin.addin'))
    "Extract both the manifest and ModelHealthChecker folder into %APPDATA%\Autodesk\Revit\Addins\$($artifact.Year)\. This package is only for Revit $($artifact.Year). Close Revit before replacing files." | Set-Content (Join-Path $zipRoot 'INSTALL.txt')
    $archive = Join-Path $releases "ModelHealthChecker-$Version-Revit$($artifact.Year).zip"
    Compress-Archive -Path "$zipRoot/*" -DestinationPath $archive -Force
    $archives += $archive
}
& dotnet (Join-Path $package 'Modena.RevitAddin.Updater.dll') --validate-package $package
if ($LASTEXITCODE -ne 0) { throw 'Package binary API/runtime validation failed.' }
if (-not $SkipVelopack) {
    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) { throw 'Install the Velopack CLI with: dotnet tool install -g vpk' }
    # A clean workspace permits rebuilding the same version without mixing old payloads.
    # Publish full packages; historical releases remain in the distribution directory.
    $velopackStage = Join-Path $output 'velopack'
    Reset-ArtifactDirectory $velopackStage $output
    & vpk pack --packId ModelHealthChecker --packVersion $Version --packDir $package --mainExe Modena.RevitAddin.Updater.exe --outputDir $velopackStage --framework net8.0-x64-desktop
    if ($LASTEXITCODE -ne 0) { throw 'Velopack pack failed.' }
    Copy-Item -Path "$velopackStage/*" -Destination $releases -Force
    $obsoleteDelta = Join-Path $releases "ModelHealthChecker-$Version-delta.nupkg"
    if (Test-Path -LiteralPath $obsoleteDelta) { Remove-Item -LiteralPath $obsoleteDelta }
}
& "$PSScriptRoot/Verify-Packages.ps1" -Version $Version -SkipVelopack:$SkipVelopack
if ($PublishToGitHub) {
    if ($SkipVelopack) { throw 'Publishing requires a complete Velopack release.' }
    if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN is required to publish.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI is required to upload the manual ZIPs.' }
    & vpk upload github --repoUrl https://github.com/givenxmodena/ModelHealthChecker --token $env:GITHUB_TOKEN --publish --releaseName "v$Version" --tag "v$Version" --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'Velopack GitHub upload failed.' }
    & gh release upload "v$Version" @archives --repo givenxmodena/ModelHealthChecker
    if ($LASTEXITCODE -ne 0) { throw 'Manual ZIP upload failed.' }
}
Write-Host "Release ${Version}: four year-specific ZIPs and payloads created. Velopack skipped: $SkipVelopack"
