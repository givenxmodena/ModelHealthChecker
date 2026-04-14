# Build-Release.ps1
# Builds and packages the Modena Model Health Checker for release via Velopack.
#
# Prerequisites:
#   1. Install Velopack CLI:  dotnet tool install -g vpk
#   2. Revit 2024 API available for the Legacy (net48) build (optional)
#
# Usage:
#   .\scripts\Build-Release.ps1 -Version "1.0.1"
#   .\scripts\Build-Release.ps1 -Version "1.0.1" -PublishToGitHub
#
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$PublishToGitHub,

    [string]$GitHubToken = $env:GITHUB_TOKEN
)

$ErrorActionPreference = "Stop"

# ── Paths ────────────────────────────────────────────────────────────────────
$RepoRoot      = Split-Path -Parent $PSScriptRoot
$BuildOutputDir = Join-Path $RepoRoot "build-output"
$ReleasesDir   = Join-Path $RepoRoot "releases"
$AppId         = "ModelHealthChecker"
$GitHubRepo    = "givenxmodena/ModelHealthChecker"

$Net8OutputDir  = Join-Path $BuildOutputDir "net8.0-windows"
$Net48OutputDir = Join-Path $BuildOutputDir "net48"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Model Health Checker Release Builder v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# ── Step 1: Clean ────────────────────────────────────────────────────────────
Write-Host "`n[1/6] Cleaning previous build output..." -ForegroundColor Yellow
if (Test-Path $BuildOutputDir) { Remove-Item $BuildOutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $Net8OutputDir  -Force | Out-Null
New-Item -ItemType Directory -Path $Net48OutputDir -Force | Out-Null
if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir | Out-Null }
Write-Host "Clean complete." -ForegroundColor Green

# ── Step 2: Build Modern (net8.0 / Revit 2025-2026) ─────────────────────────
Write-Host "`n[2/6] Publishing Modern build (net8.0-windows / Revit 2025-2026)..." -ForegroundColor Yellow

dotnet publish "$RepoRoot\src\Modena.RevitAddin\Modena.RevitAddin.csproj" `
    -c Release -r win-x64 --self-contained false `
    -o $Net8OutputDir
if ($LASTEXITCODE -ne 0) { throw "Modern publish failed." }
Write-Host "Modern publish succeeded." -ForegroundColor Green

# ── Step 3: Build Legacy (net48 / Revit 2024) ────────────────────────────────
Write-Host "`n[3/6] Building Legacy build (net48 / Revit 2024)..." -ForegroundColor Yellow

$LegacyApiDir = $env:REVIT_LEGACY_API_DIR
if (-not $LegacyApiDir -and (Test-Path 'C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll')) {
    $LegacyApiDir = 'C:\Program Files\Autodesk\Revit 2024'
}

if (-not $LegacyApiDir) {
    Write-Host "Revit 2024 API not found — skipping Legacy build (set REVIT_LEGACY_API_DIR to override)." -ForegroundColor Yellow
} else {
    dotnet build "$RepoRoot\src\Modena.RevitAddin.Legacy\Modena.RevitAddin.Legacy.csproj" `
        -c Release -p:Platform=x64 `
        "-p:RevitLegacyApiDir=$LegacyApiDir" `
        -o $Net48OutputDir
    if ($LASTEXITCODE -ne 0) { throw "Legacy build failed." }
    Write-Host "Legacy build succeeded." -ForegroundColor Green
}

# ── Step 4: Build Updater (copies framework subfolders into package root) ────
Write-Host "`n[4/6] Building Updater (Velopack entry point)..." -ForegroundColor Yellow

dotnet build "$RepoRoot\src\Modena.RevitAddin.Updater\Modena.RevitAddin.Updater.csproj" `
    -c Release
if ($LASTEXITCODE -ne 0) { throw "Updater build failed." }
Write-Host "Updater build succeeded." -ForegroundColor Green

# ── Step 5: Package with Velopack ────────────────────────────────────────────
Write-Host "`n[5/6] Packaging with Velopack..." -ForegroundColor Yellow

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    Write-Host "Installing Velopack CLI..." -ForegroundColor Yellow
    dotnet tool install -g vpk
}

vpk pack `
    --packId    $AppId `
    --packVersion $Version `
    --packDir   $Net8OutputDir `
    --mainExe   "Modena.RevitAddin.Updater.exe" `
    --outputDir $ReleasesDir `
    --framework "net8.0-x64-desktop"

if ($LASTEXITCODE -ne 0) { throw "Velopack pack failed." }
Write-Host "Velopack packaging complete." -ForegroundColor Green

# ── Step 6: Create convenience ZIP archives ───────────────────────────────────
Write-Host "`n[6/6] Creating ZIP archives for manual distribution..." -ForegroundColor Yellow

# Revit 2025-2026 (net8.0)
$Zip2026 = Join-Path $ReleasesDir "ModelHealthChecker-$Version-Revit2025-2026.zip"
Compress-Archive -Path @(
    (Join-Path $Net8OutputDir "Modena.RevitAddin.dll"),
    (Join-Path $Net8OutputDir "Modena.Shared.dll"),
    (Join-Path $Net8OutputDir "Newtonsoft.Json.dll"),
    (Join-Path $Net8OutputDir "Velopack.dll"),
    (Join-Path $Net8OutputDir "NuGet.Versioning.dll"),
    (Join-Path $Net8OutputDir "Modena.RevitAddin.Updater.exe"),
    (Join-Path $Net8OutputDir "modena-config.json"),
    (Join-Path $RepoRoot "src\Modena.RevitAddin\Modena.RevitAddin.addin")
) -DestinationPath $Zip2026 -Force
Write-Host "Created: $Zip2026" -ForegroundColor Green

# Revit 2024 (net48) — only if legacy build ran
$Zip2024 = Join-Path $ReleasesDir "ModelHealthChecker-$Version-Revit2024.zip"
if (Test-Path (Join-Path $Net48OutputDir "Modena.RevitAddin.dll")) {
    Compress-Archive -Path @(
        (Join-Path $Net48OutputDir "Modena.RevitAddin.dll"),
        (Join-Path $Net48OutputDir "Modena.Shared.dll"),
        (Join-Path $Net48OutputDir "Newtonsoft.Json.dll"),
        (Join-Path $Net48OutputDir "modena-config.json"),
        (Join-Path $RepoRoot "src\Modena.RevitAddin\Modena.RevitAddin.addin")
    ) -DestinationPath $Zip2024 -Force
    Write-Host "Created: $Zip2024" -ForegroundColor Green
} else {
    Write-Host "Skipped Revit 2024 ZIP (Legacy build was skipped)." -ForegroundColor Yellow
}

# ── Summary ──────────────────────────────────────────────────────────────────
Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "Release $Version build complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "`nArtifacts in: $ReleasesDir" -ForegroundColor Yellow

$artifacts = @(
    "ModelHealthChecker-$Version-full.nupkg",
    "ModelHealthChecker-$Version-delta.nupkg",
    "RELEASES",
    "ModelHealthChecker-win-Setup.exe",
    "ModelHealthChecker-win-Portable.zip",
    "ModelHealthChecker-$Version-Revit2025-2026.zip",
    "ModelHealthChecker-$Version-Revit2024.zip"
)

foreach ($a in $artifacts) {
    $path = Join-Path $ReleasesDir $a
    if (Test-Path $path) {
        Write-Host "  [OK] $a" -ForegroundColor Green
    } else {
        $color = if ($a -match "delta|Revit2024") { "Gray" } else { "Red" }
        Write-Host "  [--] $a" -ForegroundColor $color
    }
}

# ── Optional: Publish to GitHub ───────────────────────────────────────────────
if ($PublishToGitHub) {
    Write-Host "`nPublishing to GitHub Releases..." -ForegroundColor Yellow

    if (-not $GitHubToken) {
        throw "GitHub token required. Set GITHUB_TOKEN env var or pass -GitHubToken."
    }

    $toUpload = @()
    foreach ($a in $artifacts) {
        $p = Join-Path $ReleasesDir $a
        if (Test-Path $p) { $toUpload += $p }
    }

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($gh) {
        gh release create "v$Version" @toUpload `
            --repo  $GitHubRepo `
            --title "v$Version" `
            --notes "Modena Model Health Checker v$Version"
        Write-Host "Published to GitHub!" -ForegroundColor Green
    } else {
        Write-Warning "GitHub CLI (gh) not found. Upload artifacts manually from: $ReleasesDir"
    }
} else {
    Write-Host "`nSkipping GitHub publish (add -PublishToGitHub to enable)." -ForegroundColor Gray
}
