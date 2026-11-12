$RevitArtifacts = @(
    [pscustomobject]@{ Year = '2024'; Configuration = 'R24'; Framework = 'net48'; Folder = 'net48-2024'; Project = 'src/Modena.RevitAddin.Legacy/Modena.RevitAddin.Legacy.csproj' },
    [pscustomobject]@{ Year = '2025'; Configuration = 'R25'; Framework = 'net8.0-windows'; Folder = 'net8.0-windows-2025'; Project = 'src/Modena.RevitAddin/Modena.RevitAddin.csproj' },
    [pscustomobject]@{ Year = '2026'; Configuration = 'R26'; Framework = 'net8.0-windows'; Folder = 'net8.0-windows-2026'; Project = 'src/Modena.RevitAddin/Modena.RevitAddin.csproj' },
    [pscustomobject]@{ Year = '2027'; Configuration = 'R27'; Framework = 'net10.0-windows'; Folder = 'net10.0-windows-2027'; Project = 'src/Modena.RevitAddin/Modena.RevitAddin.csproj' }
)

function Assert-RevitPayload($Path, $Artifact) {
    foreach ($file in @('Modena.RevitAddin.dll', 'Modena.Shared.dll', 'Newtonsoft.Json.dll',
        'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.Wpf.dll', 'WebView2Loader.dll',
        'MLBS.Desktop.Client.dll', 'System.Security.Cryptography.ProtectedData.dll',
        'modena-config.json', 'Registry/ModelHealthChecker.json', 'revit-build.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $file))) { throw "Missing $($Artifact.Year) payload file: $file" }
    }
    if ($Artifact.Year -eq '2024') {
        foreach ($file in @('System.Text.Json.dll', 'System.Net.Http.Json.dll',
            'Microsoft.Bcl.AsyncInterfaces.dll', 'System.Memory.dll', 'System.Buffers.dll',
            'System.Numerics.Vectors.dll', 'System.Runtime.CompilerServices.Unsafe.dll',
            'System.Text.Encodings.Web.dll', 'System.Threading.Tasks.Extensions.dll', 'System.ValueTuple.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Path $file))) { throw "Missing legacy MLBS dependency: $file" }
        }
    }
    $identity = Get-Content -LiteralPath (Join-Path $Path 'revit-build.json') -Raw | ConvertFrom-Json
    if ($identity.revitYear -ne $Artifact.Year -or $identity.targetFramework -ne $Artifact.Framework -or
        $identity.apiMajor -ne ([int]$Artifact.Year - 2000)) { throw "Wrong Revit payload in $Path" }
    if (Test-Path -LiteralPath (Join-Path $Path 'RevitAPI.dll')) { throw "Revit API must not be distributed: $Path" }
}

function Reset-ArtifactDirectory([string]$Path, [string]$Root) {
    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside output root $resolvedRoot : $resolvedPath"
    }
    if (Test-Path -LiteralPath $resolvedPath) { Remove-Item -LiteralPath $resolvedPath -Recurse -Force }
    New-Item -ItemType Directory -Path $resolvedPath -Force | Out-Null
}
