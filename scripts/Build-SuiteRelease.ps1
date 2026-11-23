param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pulse = Test-Path (Join-Path $repo 'src/Modena.RevitAddin/Modena.RevitAddin.csproj')
$id = if ($pulse) { 'ModelHealthChecker' } else { 'Exportly' }
$assembly = if ($pulse) { 'Modena.RevitAddin.dll' } else { 'OneExport.dll' }
$productCode = if ($pulse) { '50554C5345' } else { '4558504F52544C59' }
$repository = if ($pulse) { 'givenxmodena/ModelHealthChecker' } else { 'modena-aec/OneSync' }
$output = Join-Path $repo "build-output/suite-release-$Version"
if (Test-Path -LiteralPath $output) { throw "Use a new release output: $output" }
$assets = Join-Path $output 'assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$descriptorTemplate = Get-Content (Join-Path $repo 'integration/modena-module.json') -Raw | ConvertFrom-Json
$notes = Join-Path $repo "docs/release-notes/v$Version.md"
$releaseInformation = Join-Path $repo 'release-information.json'
$inputs = @()
Add-Type -AssemblyName System.Reflection.Metadata
foreach ($year in '2024','2025','2026','2027') {
    $framework = if ($year -eq '2024') { 'net48' } elseif ($year -eq '2027') { 'net10.0-windows' } else { 'net8.0-windows' }
    if ($pulse) {
        $folder = if ($year -eq '2024') { 'net48-2024' } else { "$framework-$year" }
        $source = Join-Path $repo "publish/Installer/$folder"
        if (-not $SkipBuild) { throw 'Run scripts/Build-Release.ps1 first, then use -SkipBuild.' }
    } else {
        $source = Join-Path $repo "build-output/release-$Version/payloads/$year"
        if (-not $SkipBuild) {
            $project = if ($year -eq '2024') { 'src/OneExport.Legacy/OneExport.Legacy.csproj' } else { 'src/OneExport/OneExport.csproj' }
            & dotnet publish (Join-Path $repo $project) -c Release -p:Platform=x64 -p:ExportlyFlavor=Mlbs "-p:RevitVersion=$year" "-p:Version=$Version" "-p:AssemblyVersion=$Version.0" "-p:FileVersion=$Version.0" -p:ExportlyMlbsApiBaseUrl=https://mms.modena-app.co.za/mlbsapi/ -p:DeployToRevitOnBuild=false -o $source
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $year" }
        }
    }
    $destination = Join-Path $output "Runtime/$year/Modules/$id"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Where-Object {
        $_.Extension -notin '.addin','.pdb' -and $_.Name -notin 'aps.json','RevitAPI.dll','RevitAPIUI.dll','AdWindows.dll'
    } | Copy-Item -Destination $destination -Recurse -Force
    # Desktop APS credentials are provisioned by the Suite environment, not the release archive.
    foreach ($file in Get-ChildItem -LiteralPath $destination -Recurse -Filter '*.json' -File) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $text = [regex]::Replace($text, '(?i)("(?:ApsClientSecret|APS_CLIENT_SECRET(?:_X)?|clientSecret|accessToken|password)"\s*:\s*)"[^"]*"', '$1""')
        [IO.File]::WriteAllText($file.FullName, $text, [Text.UTF8Encoding]::new($false))
    }
    $dll = Join-Path $destination $assembly
    $stream = [IO.File]::OpenRead($dll)
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $definition = $reader.GetAssemblyDefinition()
        if ($definition.Version.ToString(3) -ne $Version) { throw "Wrong module version: $year" }
        $refs = @($reader.AssemblyReferences | ForEach-Object { $reader.GetAssemblyReference($_) })
        $api = @($refs | Where-Object { $reader.GetString($_.Name) -eq 'RevitAPI' })
        if ($api.Count -ne 1 -or $api[0].Version.Major -ne ([int]$year - 2000)) { throw "Wrong Revit API: $year" }
        if (-not @($refs | Where-Object { $reader.GetString($_.Name) -eq 'MLBS.Desktop.Client' }).Count) { throw "Missing MLBS reference: $year" }
        $trialDays = $null
        foreach ($handle in $reader.TypeDefinitions) {
            $type = $reader.GetTypeDefinition($handle)
            if ($reader.GetString($type.Name) -ne 'TrialManager') { continue }
            foreach ($fieldHandle in $type.GetFields()) {
                $field = $reader.GetFieldDefinition($fieldHandle)
                if ($reader.GetString($field.Name) -eq 'TRIAL_DAYS') {
                    $constant = $reader.GetConstant($field.GetDefaultValue())
                    $trialDays = [BitConverter]::ToInt32($reader.GetBlobBytes($constant.Value), 0)
                }
            }
        }
        if ($trialDays -ne 30) { throw "Expected compiled 30-day trial: $year" }
    } finally { $pe.Dispose(); $stream.Dispose() }
    if (-not (Test-Path (Join-Path $destination 'MLBS.Desktop.Client.dll'))) { throw "Missing MLBS bundle: $year" }
    $descriptor = $descriptorTemplate | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $descriptor.moduleVersion = $Version
    $descriptor.compatibility.supportedRevitVersions = @($year)
    $descriptor | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $destination 'modena-module.json') -Encoding utf8
    Copy-Item -LiteralPath $releaseInformation -Destination $destination
    $policy = @{ trialDays=30; usageLimit=1000; requestTimeoutSeconds=30; productCode=$productCode; apiBaseUrl='https://mms.modena-app.co.za/mlbsapi/'; credentialProvisioning='Existing APS environment variables or external aps.json'; nativeAcceptance='Pending' }
    $policy | ConvertTo-Json | Set-Content (Join-Path $destination 'release-policy.json') -Encoding utf8
    $catalog = Join-Path $output "Runtime/$year/Catalog/modules"
    New-Item -ItemType Directory -Path $catalog -Force | Out-Null
    Copy-Item (Join-Path $destination 'modena-module.json') (Join-Path $catalog "$id.json")
    Compress-Archive -Path "$destination/*" -DestinationPath (Join-Path $assets "$id-$Version-suite-Revit$year.zip")
    $inputs += @{ year=$year; framework=$framework; assembly=$assembly; sha256=(Get-FileHash $dll).Hash; mlbsSha256=(Get-FileHash (Join-Path $destination 'MLBS.Desktop.Client.dll')).Hash; source=$source }
    Write-Host "Verified $id $Version / Revit $year: compiled 30-day trial, exact API and MLBS dependency."
}
Copy-Item -LiteralPath $notes -Destination (Join-Path $assets 'RELEASE-NOTES.md')
Compress-Archive -Path (Join-Path $output 'Runtime') -DestinationPath (Join-Path $assets "$id-$Version-suite-payload.zip")
@{ moduleId=$id; version=$Version; repository="https://github.com/$repository"; sourceCommit=(git -C $repo rev-parse HEAD); payloads=$inputs; signed=$false } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $assets 'release-manifest.json') -Encoding utf8
Get-ChildItem $assets -File | Sort-Object Name | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName).Hash.ToLowerInvariant(), $_.Name } | Set-Content (Join-Path $assets 'SHA256SUMS.txt') -Encoding utf8
Write-Host "Suite release assets: $assets"
