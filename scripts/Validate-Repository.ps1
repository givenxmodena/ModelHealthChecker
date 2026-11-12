$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$files = & rg --files --hidden -g '!**/.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!publish/**' -g '!build-output/**' -g '!releases/**' $repo
foreach ($file in $files) {
    if ($file.EndsWith('.ps1')) {
        $tokens = $null; $errors = $null
        [void][Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ($errors | Out-String) }
    }
    if ($file -match '\.(csproj|props|targets|addin|manifest)$' -or $file.EndsWith('.addin.template')) {
        [xml]$xml = Get-Content -LiteralPath $file -Raw
    }
    if ($file.EndsWith('.json')) { Get-Content -LiteralPath $file -Raw | ConvertFrom-Json | Out-Null }
}
Write-Host 'All repository PowerShell, project XML, manifests, and JSON parsed successfully.'
