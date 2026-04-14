$ErrorActionPreference = 'Stop'

Write-Host "Building Modena Model Health Checker for Revit 2026..." -ForegroundColor Yellow

# Build the add-in project (PostBuild target deploys .addin to Revit Addins folder)
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c Debug -v minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Verify deployment
$addinPath = "$env:APPDATA\Autodesk\Revit\Addins\2026\Modena.RevitAddin.addin"
if (Test-Path $addinPath) {
    Write-Host "Verified: .addin deployed to $addinPath" -ForegroundColor Green
} else {
    Write-Host "WARNING: .addin file not found at $addinPath" -ForegroundColor Yellow
}

# Launch Revit 2026
Write-Host ""
Write-Host "Launching Revit 2026..." -ForegroundColor Yellow

$revitPath = "C:\Program Files\Autodesk\Revit 2026\Revit.exe"
if (Test-Path $revitPath) {
    Start-Process $revitPath -ArgumentList "/language ENU"
    Write-Host "Revit 2026 launched successfully!" -ForegroundColor Green
} else {
    Write-Host "ERROR: Revit 2026 not found at $revitPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Wait for Revit to fully load" -ForegroundColor White
Write-Host "2. Open a Revit model" -ForegroundColor White
Write-Host "3. Click the 'Modena' tab in the ribbon" -ForegroundColor White
Write-Host "4. Click 'Model Health Checker' button" -ForegroundColor White
Write-Host "5. Ensure the backend API is running (dotnet run in Modena.Api)" -ForegroundColor White
