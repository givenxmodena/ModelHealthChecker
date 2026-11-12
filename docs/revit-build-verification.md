# Revit-year build verification — 2026-09-17

Status: implementation and automated verification complete. Revit was not launched. Interactive Revit and installer lifecycle tests remain manual, as requested. Nothing was published to GitHub.

## Final contract

| Year | Runtime | Installed RevitAPI / RevitAPIUI identity | Payload |
|---|---|---|---|
| 2024 | .NET Framework 4.8 | 24.3.40.0 / 24.3.40.0 | net48-2024 |
| 2025 | .NET 8 | 25.4.50.0 / 25.4.50.0 | net8.0-windows-2025 |
| 2026 | .NET 8 | 26.1.0.0 / 26.1.0.0 | net8.0-windows-2026 |
| 2027 | .NET 10 | 27.1.0.0 / 27.1.0.0 | net10.0-windows-2027 |

Each API comes from `C:\Program Files\Autodesk\Revit <year>`. Both the project references and compiled DLL metadata were verified. API DLLs are non-copy-local and excluded from distribution. R25 and R26 DLLs have different SHA256 hashes. Shared DTO assemblies are independent of Revit and may share a runtime target.

Reference inspected: Model Info Updater's project configurations, installer entries and updater mappings in the adjacent repository. This implementation follows its exact-year selection while rejecting missing-payload fallbacks and keeping packaging separate from ordinary builds.

## Verification commands and results

Commands were run from the repository root using PowerShell. Log files are local, ignored build evidence under `logs/`.

1. `dotnet --list-sdks` — found 8.0.425 and 10.0.401 (also 6.0.428 and 9.0.301). Installed API identities were read with `[Reflection.AssemblyName]::GetAssemblyName(<path>).Version`.

2. Independent Release project builds — all passed, zero errors:

   ```powershell
   dotnet build src/Modena.RevitAddin.Legacy/Modena.RevitAddin.Legacy.csproj -c 'Release R24' -p:Platform=x64 -v minimal
   dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R25' -p:Platform=x64 -v minimal
   dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R26' -p:Platform=x64 -v minimal
   dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R27' -p:Platform=x64 -v minimal
   ```

   Evidence: `logs/build-R24.log` through `logs/build-R27.log`.

3. All eight solution configurations, each built independently — all passed, zero errors:

   ```powershell
   foreach ($mode in @('Debug','Release')) {
       foreach ($year in 24..27) {
           dotnet build ModenaModelHealthChecker.sln -c "$mode R$year" -p:Platform=x64 -v minimal
           if ($LASTEXITCODE -ne 0) { throw "$mode R$year failed" }
       }
   }
   ```

   Evidence: `logs/solution-Debug-R24.log` through `logs/solution-Release-R27.log`. Earlier Debug-only runs are in `logs/debug-R24.log` through `logs/debug-R27.log`.

4. Evaluated configuration properties and installed APIs — passed for all eight configurations:

   ```powershell
   . ./scripts/RevitArtifacts.ps1
   foreach ($a in $RevitArtifacts) {
       foreach ($mode in @('Debug','Release')) {
           dotnet msbuild $a.Project "-p:Configuration=$mode $($a.Configuration)" -p:Platform=x64 -getProperty:RevitYear,TargetFramework,OutputPath,DefineConstants,Optimize,RevitApiDir
       }
   }
   ```

   Results were checked against each year's expected runtime, folder and REVIT symbol. API assembly versions were checked against the corresponding year. Evidence: `logs/api-matrix.log`.

5. `./scripts/Build-Release.ps1 -Version 1.0.5` — passed. Internally invokes `./build-multi-version.ps1 -Version 1.0.5`, all four exact Release builds, and:

   ```powershell
   dotnet publish src/Modena.RevitAddin.Updater/Modena.RevitAddin.Updater.csproj -c Release -r win-x64 --self-contained false -o build-output/package -p:Version=1.0.5 -p:AssemblyVersion=1.0.5.0 -p:FileVersion=1.0.5.0 -p:AssemblyInformationalVersion=1.0.5
   dotnet build-output/package/Modena.RevitAddin.Updater.dll --validate-package build-output/package
   vpk pack --packId ModelHealthChecker --packVersion 1.0.5 --packDir build-output/package --mainExe Modena.RevitAddin.Updater.exe --outputDir build-output/velopack --framework net8.0-x64-desktop
   ```

   The four internal add-in builds receive the same version properties and `-p:DeployToRevit=false`. All four staging folders, four manual ZIPs, full nupkg, portable ZIP and Velopack setup were produced. Evidence: `logs/release-final.log`. Earlier successful build: `logs/release.log`. The current pipeline uses a clean Velopack workspace, emits full packages, and does not generate deltas. Historical release files are preserved; a stale delta for the version being rebuilt is removed.

6. `dotnet build src/Modena.RevitAddin.Updater/Modena.RevitAddin.Updater.csproj -c Release -v minimal` — passed, zero warnings/errors. Evidence: `logs/updater.log`.

7. `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj -c Debug -v minimal` — **18 passed, 0 failed, 0 skipped**. Includes all existing property tests plus mapping, unsupported-year, no-fallback, renamed wrong-year payload, staged runtime/API, installer route/uninstall and manual ZIP checks. Evidence: `logs/tests-final.log`. Tests require staging/ZIP creation first; no Revit process is needed.

8. `./scripts/Verify-Packages.ps1 -Version 1.0.5` — passed. The installer staging DLL, updater package DLL, manual ZIP DLL, full Velopack nupkg DLL and portable ZIP DLL are byte-for-byte equal for each year, with four distinct year hashes. Also invoked by the release script before any optional publishing. Evidence: `logs/artifact-hashes.log`.

9. `./scripts/Validate-Repository.ps1` — passed. Parses every source PowerShell script, csproj, props, targets, manifest and JSON file. Build-output folders are excluded.

10. `& 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' installer/ModenaModelHealthChecker-Setup.iss` — passed. Evidence: `logs/installer-final.log`. Output: `installer/installer-output/ModenaModelHealthChecker-Setup-1.0.5.exe`. Installer was compiled, not executed.

11. Expected-failure checks — passed by rejecting incorrect inputs:

    ```powershell
    dotnet msbuild src/Modena.RevitAddin/Modena.RevitAddin.csproj '-p:Configuration=Release R25' -p:Platform=x64 '-p:RevitApiDir=C:/Program Files/Autodesk/Revit 2026' -t:ValidateRevitApi -v:minimal
    dotnet msbuild src/Modena.RevitAddin/Modena.RevitAddin.csproj '-p:Configuration=Release R25' -p:TargetFramework=net10.0-windows -t:ValidateRevitApi -v:minimal
    ./src/Modena.RevitAddin/Deploy-ToSuite.ps1 -AppId ModelHealthChecker -SourceDir './publish/Installer/net8.0-windows-2026' -RevitYear 2025
    ```

    First two returned nonzero with explicit wrong-API / wrong-runtime errors. Suite deployment threw before creating any deployment directories. Evidence: `logs/wrong-api-negative.log`, `logs/wrong-runtime-negative.log` and tool output. These are intentional negative-test errors, not unresolved build failures.

12. Stale-mapping search — no source/documentation matches:

    ```powershell
    rg -n '2025-2026|2024-2026|Installer[\\/](Modern|Legacy)|RevitFrameworkMap|Falling back to root|2025.*4\.8|2025.*2024.*DLL' --glob '!logs/**' --glob '!docs/revit-build-verification.md' .
    git -c core.safecrlf=false diff --check
    ```

    `rg` returned 1 (no matches); final whitespace check returned 0. Existing ignored historical release archives retain their historical filenames; they are not inputs to the new package.

13. `vpk upload github --help` — checked the installed CLI's upload flags. No upload/publish command was executed.

## Resolved verification failures

- Initial script parser error around a variable immediately followed by a colon was corrected using `${Version}`.
- Initial test restore failed with NETSDK1004 because NuGet did not restore the AdditionalProperties no-trial/configuration variant into its newly isolated assets path. Identical trial/no-trial dependencies now share a configuration-specific restore path while compiler/output files remain isolated; subsequent full tests passed.
- Rebuilding version 1.0.5 into a populated Velopack output directory was rejected by vpk, leaving an older package beside new staging outputs. Packaging now uses a clean workspace and copies successful results to `releases`. Final full/portable/manual archive hash verification passed.
- Whitespace-at-EOF findings were corrected; final diff check passed.

## Warnings (separate from errors)

There are no outstanding automated build/test/package errors.

- R24 emitted CS8603 nullable-return warnings in existing AuthService/RevitContextService and CS0618 for existing ElementId APIs. WPF temporary/final compilation reports these twice (8 warnings in the fresh configuration builds).
- R25/R26/R27 emitted MSB3277 conflicts from installed Autodesk transitive dependencies involving Microsoft.VisualBasic and System.Drawing (2 warnings per configuration). These were not suppressed or treated as evidence of a wrong-year reference. Runtime smoke tests remain necessary.
- Velopack artifacts are unsigned; vpk also reported a newer CLI version available. No tool upgrade was performed.
- Inno reports deprecated `x64`/`pf` aliases and the pre-existing admin/per-user destination mismatch. Installation under a different administrator account must be tested; deployment paths are per-user. This change preserves the existing privilege model.
- Existing installer APS credential embedding/obfuscation remains unchanged; no credentials are reproduced in this report.
- Git reports LF-to-CRLF normalization notices under the existing repository settings.

## Manual validation still required

Owner: maintainer/release tester. Do not execute these automatically.

For each of Revit 2024, 2025, 2026 and 2027: install its package, confirm one ribbon entry and the correct assembly path, open representative local/cloud models, load/refresh the dashboard, inspect warnings and family extraction, test authentication/WebView2, and close/reopen the window. Confirm trial/non-trial behavior as applicable and inspect logs for dependency/load failures.

On a disposable target machine: test fresh install, upgrade from the old flat layout, uninstall/reinstall, custom Revit install paths, multiple installed years, missing-year detection, different elevation credentials, locked files, and preservation of other add-ins. Verify manifests and owned ModelHealthChecker folders are removed for all four years on uninstall. Test the updater with Revit closed and verify each installed year's deployed DLL. Velopack update/download and restart flows were not exercised.

## Review and scope

Reviewed project/solution mappings, full dependency copying, installer detection/manifest/cleanup, updater fail-closed routing, Suite deployment, package contents, and release repeatability. Resolved findings include shared-year routing, missing-year skips, root fallback, a hard-coded Suite path, mismatched installer AddInId, and archive/staging mismatch on repeated packaging.

Existing working-tree version 1.0.5 changes, family-warning suppression, extractor/command edits, and UI edits were preserved. No commit, GitHub publication, Revit launch, or installer execution was performed. README.md and Spec.md were previously ignored; explicit ignore exceptions now make their updates reviewable/versionable.
