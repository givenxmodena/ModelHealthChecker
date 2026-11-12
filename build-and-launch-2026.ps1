param(
    [switch]$Launch,
    [uri]$MlbsApiUrl,
    [switch]$StagingTest,
    [switch]$NewInstance
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/Modena.RevitAddin/Modena.RevitAddin.csproj'
$buildArgs = @('build', $project, '-c', 'Debug R26', '-p:Platform=x64')
$stagingVariant = if ($NewInstance) { 'mlbs-staging-' + (Get-Date -Format 'yyyyMMdd-HHmmss') } else { 'mlbs-staging' }
if ($NewInstance -and -not $StagingTest) { throw 'NewInstance is supported only for isolated staging tests.' }
if ($MlbsApiUrl) {
    if (-not $MlbsApiUrl.IsAbsoluteUri -or $MlbsApiUrl.Scheme -ne 'https') { throw 'MlbsApiUrl must be an absolute HTTPS URL.' }
    $buildArgs += "-p:PulseMlbsApiBaseUrl=$($MlbsApiUrl.AbsoluteUri.TrimEnd('/'))/"
}
if ($StagingTest) {
    if (-not $MlbsApiUrl -or $MlbsApiUrl.Host -ne 'mmsstaging.modena-app.co.za') { throw 'StagingTest requires the MLBS staging URL.' }
    $buildArgs += "-p:BuildVariant=$stagingVariant/"
}
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Revit 2026 build failed.' }
$relativeOutput = if ($StagingTest) { "bin/Debug R26/$stagingVariant/net8.0-windows-2026" } else { 'bin/Debug R26/net8.0-windows-2026' }
$source = Join-Path (Split-Path $project) $relativeOutput
if ($StagingTest) {
    # Isolated local-model licensing test: no unrelated Autodesk login and no
    # unattended subscription-token activity from automatic refresh.
    $configPath = Join-Path $source 'modena-config.json'
    $config = Get-Content (Join-Path $PSScriptRoot 'modena-config.json') -Raw | ConvertFrom-Json
    $config | Add-Member -NotePropertyName RequireAuthentication -NotePropertyValue $false -Force
    $config | Add-Member -NotePropertyName AutoRefreshEnabled -NotePropertyValue $false -Force
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding UTF8
}
$addins = Join-Path $env:APPDATA 'Autodesk/Revit/Addins/2026'
New-Item -ItemType Directory -Path $addins -Force | Out-Null
[xml]$manifest = Get-Content (Join-Path $source 'Modena.RevitAddin.addin')
$manifest.RevitAddIns.AddIn.Assembly = Join-Path $source 'Modena.RevitAddin.dll'
$manifest.Save((Join-Path $addins 'Modena.RevitAddin.addin'))
if ($Launch) {
    if (-not $NewInstance -and (Get-Process Revit -ErrorAction SilentlyContinue)) { throw 'Revit is already running. Close it before launching with a different MLBS environment, or use an isolated StagingTest with NewInstance.' }
    $previousMlbsUrl = $env:PULSE_MLBS_API_URL
    try {
        if ($MlbsApiUrl) {
            $env:PULSE_MLBS_API_URL = $MlbsApiUrl.AbsoluteUri.TrimEnd('/') + '/'
        }
        Start-Process 'C:/Program Files/Autodesk/Revit 2026/Revit.exe' -ArgumentList '/language ENU'
    }
    finally { $env:PULSE_MLBS_API_URL = $previousMlbsUrl }
}
