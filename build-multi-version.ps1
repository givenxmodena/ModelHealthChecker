# Modena Model Health Checker Multi-Version Build Script
# Builds for both Modern (.NET 8 / Revit 2025-2026) and Legacy (.NET Framework 4.8 / Revit 2024)

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "publish"
)

Write-Host "Modena Model Health Checker Multi-Version Build" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Build Modern Version (.NET 8 for Revit 2025-2026)
Write-Host "Building Modern Version (.NET 8 for Revit 2025-2026)..." -ForegroundColor Cyan
$modernPath = Join-Path $OutputPath "Installer\Modern"
New-Item -ItemType Directory -Path $modernPath -Force | Out-Null

dotnet publish "src/Modena.RevitAddin/Modena.RevitAddin.csproj" -c $Configuration -r win-x64 --self-contained false -o $modernPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Modern publish failed!" -ForegroundColor Red
    exit 1
}

$modernDllCount = (Get-ChildItem -Path $modernPath -Filter "*.dll" | Measure-Object).Count
Write-Host "Published $modernDllCount DLL files to $modernPath" -ForegroundColor Green

# Build Legacy Version (.NET Framework 4.8 for Revit 2024)
Write-Host "Building Legacy Version (.NET Framework 4.8 for Revit 2024)..." -ForegroundColor Cyan
$legacyPath = Join-Path $OutputPath "Installer\Legacy"
New-Item -ItemType Directory -Path $legacyPath -Force | Out-Null

# Discover Legacy Revit API directory
$legacyApiDir = $env:REVIT_LEGACY_API_DIR
if (-not $legacyApiDir -and (Test-Path 'C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll')) {
    $legacyApiDir = 'C:\Program Files\Autodesk\Revit 2024'
}

$legacyBuilt = $false
if (-not $legacyApiDir) {
    Write-Host "Legacy Revit API not found: set REVIT_LEGACY_API_DIR to the Revit 2024 install folder. Skipping Legacy build." -ForegroundColor Yellow
} else {
    dotnet build "src/Modena.RevitAddin.Legacy/Modena.RevitAddin.Legacy.csproj" -c $Configuration -p:Platform=x64 "/p:RevitLegacyApiDir=$legacyApiDir"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Legacy build failed! Continuing with Modern only." -ForegroundColor Yellow
    } else {
        $legacyBuildOutput = "src/Modena.RevitAddin.Legacy/bin/x64/$Configuration/net48"
        if (!(Test-Path $legacyBuildOutput)) {
            $legacyBuildOutput = "src/Modena.RevitAddin.Legacy/bin/$Configuration/net48"
        }
        if (Test-Path $legacyBuildOutput) {
            Copy-Item -Path (Join-Path $legacyBuildOutput "*") -Destination $legacyPath -Recurse -Force
            $legacyBuilt = $true
            $legacyDllCount = (Get-ChildItem -Path $legacyPath -Filter "*.dll" | Measure-Object).Count
            Write-Host "Built $legacyDllCount DLL files to $legacyPath" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Build Summary:" -ForegroundColor Green
Write-Host "  Modern (.NET 8):           Revit 2025-2026  ->  $modernPath" -ForegroundColor White
if ($legacyBuilt) {
    Write-Host "  Legacy (.NET Framework 4.8): Revit 2024      ->  $legacyPath" -ForegroundColor White
} else {
    Write-Host "  Legacy: SKIPPED (Revit 2024 API not available)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Next: Compile installer\ModenaModelHealthChecker-Setup.iss in Inno Setup" -ForegroundColor Cyan
Write-Host "Build completed!" -ForegroundColor Green
