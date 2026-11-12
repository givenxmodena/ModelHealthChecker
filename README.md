# Modena Model Health Checker

Pulse is a Revit add-in for model health checks, local scan history and optional
ACC published-model information. See [ACC cloud integration](docs/acc-cloud-integration.md)
for current authentication, fallback and live-validation limits.
See [MLBS trial and activation](docs/mlbs-integration.md) for licensing and the
PULSE product code and staging test setup. Backend/API
sections below describe the original design; those projects are not present in
this checkout. Current health checks read the open Revit document directly.

## Solution Structure

```
src/
  Modena.RevitAddin/     Revit add-in with WPF dashboard
  Modena.Api/            ASP.NET Core backend API
  Modena.Shared/         Shared DTOs and enums
tests/
  Modena.Api.Tests/      Backend property and unit tests
  Modena.RevitAddin.Tests/ Add-in property and unit tests
```

## Data Flow

The system follows this flow for cloud model health analysis:

1. **Revit Add-in** sends model identity (project GUID, model GUID, item ID, version URN) to the backend
2. **Backend API** calls APS Data Management API to retrieve item and version metadata
3. **Backend API** calls APS Model Derivative API to extract model properties (elements, categories, families)
4. **Backend API** computes health metrics (pass rate, failed checks, warnings) from extracted properties
5. **Backend API** stores the health snapshot in the database
6. **Backend API** returns the dashboard response to the add-in
7. **Revit Add-in** displays the health data in the WPF dashboard

For local models (non-cloud), the extraction service returns zero values with a note that local extraction is not yet supported.

## Prerequisites

Before running the system, you need:

- .NET 8 and .NET 10 SDKs, plus .NET Framework 4.8 targeting support
- Installed Revit 2024, 2025, 2026, and 2027 API assemblies for a complete release
- SQL Server (LocalDB works for development)
- **APS credentials** (required for cloud model extraction)

## Obtaining APS Credentials

To extract health data from cloud models, you must register an application with Autodesk Platform Services:

1. Go to [https://aps.autodesk.com](https://aps.autodesk.com) and sign in with your Autodesk account
2. Navigate to **My Apps** and click **Create App**
3. Select **Traditional Web App** as the application type
4. Fill in the required fields (app name, description, callback URL)
5. After creation, copy the **Client ID** and **Client Secret**

### Required APS Scopes

The application requires the following OAuth scopes:

| Scope | API | Purpose |
|-------|-----|---------|
| `data:read` | Data Management API | Read item and version metadata from ACC/BIM 360 |
| `viewables:read` | Model Derivative API | Read model properties and element data |

These scopes are configured in `appsettings.json` under `Aps:Scopes`.

## Running the Backend API

### Setup

1. **Configure APS credentials** in `src/Modena.Api/appsettings.json`:

```json
{
  "Aps": {
    "ClientId": "your-client-id-here",
    "ClientSecret": "your-client-secret-here",
    "CallbackUrl": "http://localhost:5000/api/autodesk/callback",
    "Scopes": "data:read viewables:read"
  }
}
```

The API will log a warning on startup if credentials are missing or empty.

2. Update the connection string in `src/Modena.Api/appsettings.json` if needed:

```json
{
  "ConnectionStrings": {
    "ModenaDb": "Server=(localdb)\\mssqllocaldb;Database=ModenaModelHealth;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

3. Apply EF Core migrations:

```bash
cd src/Modena.Api
dotnet ef database update
```

4. Run the API:

```bash
cd src/Modena.Api
dotnet run
```

The API starts at `http://localhost:5000` by default. The database starts empty — health data is populated when you trigger a refresh from the add-in.

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/model-health/load` | Load latest health snapshot for a model |
| POST | `/api/model-health/refresh` | Extract fresh data from APS and return updated snapshot |
| GET | `/api/model-health/health` | Health check |
| GET | `/api/model-health/history/{modelKey}` | Snapshot history for a model |

## Configuring the Add-in

The add-in reads settings from `modena-config.json`, located next to the add-in DLL. A template is provided at the repository root.

```json
{
  "ApiBaseUrl": "http://localhost:5000",
  "RefreshIntervalMinutes": 5,
  "AutoRefreshEnabled": true,
  "RequestTimeoutSeconds": 30,
  "LoggingLevel": "Information"
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `ApiBaseUrl` | Backend API base URL | `http://localhost:5000` |
| `RefreshIntervalMinutes` | Auto-refresh interval in minutes | `5` |
| `AutoRefreshEnabled` | Enable/disable auto-refresh timer | `true` |
| `RequestTimeoutSeconds` | HTTP request timeout | `30` |
| `LoggingLevel` | Logging verbosity (Debug, Information, Warning, Error) | `Information` |

## Supported Revit builds and installation

Each Revit year has its own DLL compiled against that year's installed RevitAPI.dll and RevitAPIUI.dll. Sharing a runtime does not make these DLLs interchangeable.

| Revit | Runtime | Configuration | Artifact folder |
|---|---|---|---|
| 2024 | .NET Framework 4.8 | Debug/Release R24 | `net48-2024` |
| 2025 | .NET 8 | Debug/Release R25 | `net8.0-windows-2025` |
| 2026 | .NET 8 | Debug/Release R26 | `net8.0-windows-2026` |
| 2027 | .NET 10 | Debug/Release R27 | `net10.0-windows-2027` |

Revit 2024 uses `src/Modena.RevitAddin.Legacy/Modena.RevitAddin.Legacy.csproj`, which links the shared add-in sources. The other years use `src/Modena.RevitAddin/Modena.RevitAddin.csproj`. Shared DTOs remain API-independent (`netstandard2.0` / `net8.0`). Plain Debug/Release configurations are development aliases for R24 in Legacy and R26 in the modern project; release packaging always uses explicit year configurations.

```powershell
dotnet build ModenaModelHealthChecker.sln -c 'Debug R25' -p:Platform=x64
dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R27' -p:Platform=x64
./build-multi-version.ps1
./scripts/Build-Release.ps1 -Version 1.0.5
```

Build output is under each project's `bin/<configuration>/<artifact folder>/`. Restore/WPF intermediates are isolated by configuration; the no-trial test variant is also isolated. Exact API references default to `C:\Program Files\Autodesk\Revit <year>`. An explicit `-p:RevitApiDir=<path>` is allowed only for API assemblies whose major version matches that year. No Autodesk DLLs are redistributed. Builds define `REVIT2024`, `REVIT2025`, `REVIT2026`, or `REVIT2027`; Debug configurations also define `DEBUG` and Release configurations enable optimization.

`build-multi-version.ps1` requires all four builds and creates four folders under `publish/Installer`. `Build-Release.ps1` publishes the independent .NET 8 updater to `build-output/package` and copies those four folders into it, validates the actual DLL API/runtime metadata, creates four separate manual ZIPs in `releases`, and builds full Velopack packages in a clean workspace before copying successful outputs to releases. Rebuilding the same version is supported; no delta package is generated. Run `./scripts/Verify-Packages.ps1 -Version 1.0.5` to compare staging, manual ZIPs, full packages and portable ZIPs byte-for-byte. Use `-SkipVelopack` for local staging and ZIP validation without Velopack. Publishing to GitHub is a separate explicit `-PublishToGitHub` option. The updater rejects missing or mismatched payloads; it has no root-DLL fallback.

Compile `installer/ModenaModelHealthChecker-Setup.iss` with Inno Setup 6 after staging. The installer detects each supported installed Revit executable and deploys only that year's payload. It uses the owned `ModelHealthChecker` subfolder under `%APPDATA%\Autodesk\Revit\Addins\<year>`, generates the corresponding root manifest, and removes owned folders/manifests on uninstall. Migration removes the old flat add-in DLL/manifest but leaves root dependency DLLs that may be shared with other products. The generated manifest uses the same AddInId as the source/updater manifest.

For manual installation, close Revit and extract the matching `ModelHealthChecker-<version>-Revit<year>.zip` into `%APPDATA%\Autodesk\Revit\Addins\<year>`. Keep `Modena.RevitAddin.addin` alongside the `ModelHealthChecker` folder; its relative Assembly path points into that folder. For a custom location, edit the manifest's Assembly path to the exact year's DLL and XML-escape special characters. Never point two Revit years at the same add-in DLL. All dependencies, resources, configuration, and registry descriptors must travel with their payload.

Normal builds do not deploy or launch Revit. Explicit Suite deployment uses `-p:DeployToRevit=true` and targets only the selected build year. `build-and-launch-2026.ps1` builds and registers a standalone R26 debug manifest; it launches Revit only with `-Launch`. Do not enable both standalone and Suite loading for the same product in a year.

The standalone updater requires the .NET 8 desktop runtime, including on a machine with only Revit 2024 or 2027; Velopack declares that prerequisite. Manual ZIP/Inno installations do not require the updater.

## How Refresh Works

1. User clicks the "Model Health Checker" button in the Modena ribbon tab.
2. A WPF dashboard window opens and populates the model identity from the active Revit document.
3. User clicks "Load/Refresh" to fetch health data from the backend API.
   - **First load for a new model**: Click "Refresh" to trigger APS extraction. The "Load" operation only retrieves existing snapshots from the database.
   - **Subsequent loads**: The "Load" operation returns the latest stored snapshot without calling APS APIs.
4. On first successful load (if `AutoRefreshEnabled` is true), a timer starts that refreshes data every `RefreshIntervalMinutes` minutes.
5. If a refresh is already in progress when the timer ticks, that tick is skipped.
6. When the dashboard window closes, the timer stops and all resources are disposed. No background polling occurs.

## Extending the Extraction Service

The `IModelHealthExtractionService` interface in `src/Modena.Api/Services/` defines the contract for extracting health data:

- Warnings
- Failed checks
- Family sizes
- Category counts
- Pass rate
- Custom QA rules

The `ApsExtractionService` implementation uses APS Model Derivative APIs to extract properties from cloud models. For local models, extraction returns zero values with a note that local extraction is not yet supported.

To add local Revit extraction support, implement `IModelHealthExtractionService` with direct Revit API calls.

## Running Tests

```bash
./scripts/Build-Release.ps1 -Version 1.0.5 -SkipVelopack
dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj -c Debug
./scripts/Validate-Repository.ps1
```

Tests use xUnit with FsCheck for property-based testing. The checked-in test suite is Modena.RevitAddin.Tests. Artifact integration tests require the staged package and ZIPs above; they inspect API references without loading Revit. Backend project descriptions elsewhere in this document describe the original design; those projects are not present in this checkout. See docs/revit-build-verification.md for verification evidence and pending manual checks.

## Future Extensions

Local scan history is implemented in the add-in's **History** tab, alongside the
default **Dashboard** tab. See [Health scan history](docs/health-scan-history.md)
for comparisons, storage, recovery and verification limits.

- **APS Webhooks** — Subscribe to model version events for automatic health checks on cloud model updates.
- **Cloud Automation** — Trigger health analysis pipelines when new model versions are published to ACC.
- **Full Local Revit Extraction** — Implement `IModelHealthExtractionService` to read warnings, families, and QA rules directly from the Revit API.
- **Shared History** — Optional team/cloud synchronization beyond the add-in's per-user local scan history.
- **Issue Creation** — Integrate with issue tracking systems to create tickets from failed health checks.
- **Power BI Export** — Export health snapshot data for reporting and analytics dashboards.
