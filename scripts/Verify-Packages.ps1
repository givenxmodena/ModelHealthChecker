param([Parameter(Mandatory)][string]$Version, [switch]$SkipVelopack)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/RevitArtifacts.ps1"
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ArchivedDllHash([string]$Archive, [string]$EntryName) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $entry = @($zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $EntryName })
        if ($entry.Count -ne 1) { throw "Expected one $EntryName in $Archive" }
        $stream = $entry[0].Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
    } finally { $zip.Dispose() }
}

$hashes = @()
foreach ($artifact in $RevitArtifacts) {
    $staging = Join-Path $repo "publish/Installer/$($artifact.Folder)"
    $payload = Join-Path $repo "build-output/package/$($artifact.Folder)"
    Assert-RevitPayload $staging $artifact
    Assert-RevitPayload $payload $artifact
    $hash = (Get-FileHash -LiteralPath (Join-Path $payload 'Modena.RevitAddin.dll') -Algorithm SHA256).Hash
    if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $staging 'Modena.RevitAddin.dll') -Algorithm SHA256).Hash) {
        throw "Staging differs for Revit $($artifact.Year)"
    }
    $manual = Join-Path $repo "releases/ModelHealthChecker-$Version-Revit$($artifact.Year).zip"
    if ($hash -ne (Get-ArchivedDllHash $manual 'ModelHealthChecker/Modena.RevitAddin.dll')) {
        throw "Manual ZIP differs for Revit $($artifact.Year)"
    }
    if (-not $SkipVelopack) {
        $full = Join-Path $repo "releases/ModelHealthChecker-$Version-full.nupkg"
        $portable = Join-Path $repo 'releases/ModelHealthChecker-win-Portable.zip'
        if ($hash -ne (Get-ArchivedDllHash $full "lib/app/$($artifact.Folder)/Modena.RevitAddin.dll")) {
            throw "Velopack package differs for Revit $($artifact.Year)"
        }
        if ($hash -ne (Get-ArchivedDllHash $portable "current/$($artifact.Folder)/Modena.RevitAddin.dll")) {
            throw "Portable package differs for Revit $($artifact.Year)"
        }
    }
    $hashes += $hash
    Write-Host "$($artifact.Year) $($artifact.Folder) SHA256=$hash"
}
if (@($hashes | Sort-Object -Unique).Count -ne 4) { throw 'Expected four distinct add-in DLLs.' }
Write-Host 'All year-specific staged, manual and requested Velopack artifacts match.'
