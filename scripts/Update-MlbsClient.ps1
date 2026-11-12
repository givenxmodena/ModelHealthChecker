param(
    [Parameter(Mandatory)][string]$SourceRoot
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = (Resolve-Path -LiteralPath $SourceRoot).Path
$project = Join-Path $source 'MLBS.Desktop.Client/MLBS.Desktop.Client.csproj'
if (-not (Test-Path -LiteralPath $project)) { throw "MLBS desktop client source not found: $project" }
& dotnet build $project -c Release -p:CopyLocalLockFileAssemblies=true --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'MLBS client build failed; the supplied bundles were not updated.' }
$bundles = Join-Path $repo 'libs/MLBS.Desktop.Client'
$assets = Get-Content -LiteralPath (Join-Path $source 'MLBS.Desktop.Client/obj/project.assets.json') -Raw | ConvertFrom-Json
$protectedPackage = $assets.libraries.PSObject.Properties | Where-Object Name -Like 'System.Security.Cryptography.ProtectedData/*' | Select-Object -First 1
$protectedData = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object {
    Join-Path $_ "$($protectedPackage.Value.path)/lib/net8.0/System.Security.Cryptography.ProtectedData.dll"
} | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $protectedData) { throw 'The restored .NET 8 ProtectedData package could not be resolved.' }
foreach ($pair in @(@('Modern', 'net8.0-windows'), @('Legacy', 'net48'))) {
    $output = Join-Path $source "MLBS.Desktop.Client/bin/Release/$($pair[1])"
    $destination = Join-Path $bundles $pair[0]
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $files = if ($pair[0] -eq 'Modern') {
        @((Join-Path $output 'MLBS.Desktop.Client.dll'), $protectedData)
    } else { @(Get-ChildItem -LiteralPath $output -Filter '*.dll' | Select-Object -ExpandProperty FullName) }
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing MLBS bundle dependency: $file" }
        Copy-Item -LiteralPath $file -Destination $destination -Force
    }
}
$hashes = Get-ChildItem -LiteralPath $bundles -Recurse -Filter '*.dll' | Sort-Object FullName | ForEach-Object {
    [ordered]@{ file = $_.FullName.Substring($bundles.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
ConvertTo-Json -InputObject @($hashes) | Set-Content -LiteralPath (Join-Path $bundles 'bundle-hashes.json') -Encoding UTF8
Write-Host 'Updated Modern and Legacy MLBS bundles. Run the MLBS client tests and all four Pulse builds before release.'
