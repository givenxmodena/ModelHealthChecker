You are building a production-quality MVP called "Modena Model Health Checker".

Goal
Build a Revit add-in for Autodesk Revit that appears under a ribbon tab called "Modena". The user clicks a button called "Model Health Checker". A WPF window opens and shows a dashboard template with model health information. The user clicks a Load/Refresh button to fetch the latest data. While the window remains open, the data auto-refreshes every 5 minutes. When the window closes, the timer stops. There must be no constant polling in the background when the window is closed.

This is a simple first version. Do not overengineer it. Prioritize clarity, maintainability, and clean architecture.

Primary UX
1. User opens a Revit model.
2. User goes to the Modena ribbon tab.
3. User clicks "Model Health Checker".
4. A WPF window opens.
5. The window shows:
   - model name
   - project name if available
   - last refreshed timestamp
   - health score / pass rate
   - KPI cards
   - failed checks
   - warnings
   - families
   - element categories
6. User clicks Load/Refresh.
7. The add-in fetches the latest data from an API and updates the dashboard.
8. The dashboard refreshes every 5 minutes while the window is open.
9. The timer stops when the window closes.

Important constraints
- No permanent background polling.
- No Excel as the live source of truth.
- The add-in should fetch JSON from an API.
- Keep the add-in lightweight.
- Use MVVM.
- Use async/await properly.
- Add logging and error handling.
- Design for future extension, but keep MVP simple.

Tech stack
- Revit add-in in C#
- .NET Framework or .NET version compatible with the chosen Revit version
- WPF desktop UI
- MVVM pattern
- HttpClient for API calls
- Optional WebView2 only if needed, but prefer native WPF first unless clearly justified
- Backend API in ASP.NET Core
- PostgreSQL or SQL Server for persistence
- JSON-based contracts

Architecture
Build the solution in 2 major parts:

PART A — REVIT ADD-IN
Responsibilities:
- Add the Modena ribbon tab and Model Health Checker button
- Open the WPF window
- Detect the current Revit document/model context
- Build a model identity payload
- Call backend endpoints
- Render returned data in the WPF dashboard
- Handle manual refresh
- Start and stop a 5-minute timer only while the window is open

PART B — BACKEND API
Responsibilities:
- Accept model identity and return latest health data
- Store the latest snapshot per model
- Optionally pull latest metadata for ACC/Revit Cloud models through APS
- Expose clean REST endpoints
- Return data in a dashboard-friendly shape
- Be ready for future APS webhook integration, but do not make webhooks mandatory for this MVP

What I want generated
Generate a complete starter solution with:
1. Revit add-in project
2. WPF window
3. ViewModels
4. API service layer
5. DTOs
6. Backend ASP.NET Core API
7. Example database models and migrations
8. Sample JSON payloads
9. Stubbed repositories/services
10. Clean comments describing where to plug in real extraction logic

Required add-in structure
Create these main components:

Revit Add-in
- App.cs or application entry
- RibbonSetup.cs
- ModelHealthCheckerCommand.cs (IExternalCommand)
- ModelHealthWindow.xaml
- ModelHealthWindow.xaml.cs
- ViewModels/
  - ModelHealthViewModel.cs
  - BaseViewModel.cs
- Services/
  - RevitContextService.cs
  - ModelHealthApiClient.cs
  - RefreshTimerService.cs
- Models/
  - ModelIdentity.cs
  - DashboardResponse.cs
  - SummaryDto.cs
  - FailedCheckDto.cs
  - FamilyDto.cs
  - CategoryDto.cs
  - MetricDto.cs
- Infrastructure/
  - Logging
  - Http client factory or equivalent
  - Config/settings reader

Backend API
- Controllers/
  - ModelHealthController.cs
  - AutodeskController.cs
- Services/
  - ModelHealthService.cs
  - AutodeskAuthService.cs
  - AutodeskDataService.cs
  - SnapshotRepository.cs
- Data/
  - DbContext
  - Entities
- Contracts/
  - Requests and responses
- appsettings with APS config placeholders

Required WPF dashboard layout
Create a clean professional dark-themed engineering dashboard. Native WPF is fine.
The WPF window should include:

Header section
- Title: Model Health Checker
- Model name
- Project name
- Last refreshed time
- Status text
- Load/Refresh button
- Optional auto-refresh indicator

KPI cards row
- Pass Rate
- Total Checks
- Failed Checks
- Report Only
- Warnings
- File Size or Model Elements

Main content
- Failed checks by category
- Priority breakdown
- Failed checks table
- Top families by size
- Element categories
- Passed checks list

States
- Loading state
- Empty state
- Error state
- Last successful refresh state

Behavior requirements
1. On window open:
   - Populate model context immediately
   - Do not fetch automatically unless a flag is enabled
   - Default behavior: user clicks Load/Refresh

2. On Load/Refresh:
   - Disable the button while loading
   - Show spinner/loading status
   - Send model identity to backend
   - Update UI on success
   - Show clear error message on failure

3. Auto-refresh:
   - Start only after first successful load
   - Interval: 5 minutes
   - Stop on window close
   - Avoid overlapping refresh calls
   - If a refresh is in progress, skip the next interval tick

4. Resilience:
   - Timeout API calls
   - Retry once on transient network failure
   - Log exceptions
   - Never crash Revit because of API/UI failures

Model identity contract
The add-in must build and send a model identity payload.
At minimum include:
- localModelPath or central path if available
- document title
- Revit version
- whether it is a cloud model
- projectGuid if available
- modelGuid if available
- username if appropriate
- model source enum: Local | RevitServer | ACCCloud

Example request payload:
{
  "documentTitle": "BOARDWALK SHEDS 35 (2026).rvt",
  "modelPath": "C:\\Projects\\BOARDWALK SHEDS 35 (2026).rvt",
  "revitVersion": "2026",
  "isCloudModel": false,
  "projectGuid": null,
  "modelGuid": null,
  "modelSource": "Local"
}

Backend API endpoints to implement
Implement these endpoints for the MVP:

1. POST /api/model-health/load
Purpose:
Given a model identity payload, return the latest health snapshot for that model.

Request:
{
  "documentTitle": "BOARDWALK SHEDS 35 (2026).rvt",
  "modelPath": "C:\\Projects\\BOARDWALK SHEDS 35 (2026).rvt",
  "revitVersion": "2026",
  "isCloudModel": false,
  "projectGuid": null,
  "modelGuid": null,
  "modelSource": "Local"
}

Response:
{
  "modelKey": "boardwalk-sheds-35-2026",
  "modelName": "BOARDWALK SHEDS 35 (2026).rvt",
  "projectName": "Boardwalk Sheds",
  "lastUpdatedUtc": "2026-03-13T11:10:00Z",
  "summary": {
    "passRate": 52,
    "totalChecks": 106,
    "passedChecks": 13,
    "failedChecks": 12,
    "reportOnlyChecks": 81,
    "warnings": 220,
    "fileSizeMb": 192,
    "modelElements": 7683
  },
  "failedChecks": [
    {
      "name": "Mirrored Elements",
      "count": 2164,
      "category": "Model Quality",
      "priority": "HIGH",
      "discipline": "Architecture"
    }
  ],
  "metrics": [
    { "name": "Detail Lines", "count": 9861 }
  ],
  "categories": [
    { "name": "Curtain Panels", "value": 1599 }
  ],
  "families": [
    { "name": "DR-S-SF-Sliding", "kb": 972 }
  ],
  "passedChecks": [
    "Survey Point — N/S",
    "Survey Point — E/W"
  ]
}

2. POST /api/model-health/refresh
Purpose:
Trigger a fresh data pull or recalculation, then return an updated snapshot.
For MVP, this can simply call the same service as load, optionally with forceRefresh = true.

3. GET /api/model-health/health
Purpose:
Basic API health endpoint.

4. Optional:
GET /api/model-health/history/{modelKey}
Purpose:
Return prior snapshots for future trend charts. Stub is acceptable.

Backend data model
Create a simple relational model that is enough for MVP:

Tables/entities:
- Models
  - Id
  - ModelKey
  - DocumentTitle
  - ModelPath
  - IsCloudModel
  - ProjectGuid
  - ModelGuid
  - ModelSource
  - RevitVersion
  - CreatedUtc
  - UpdatedUtc

- ModelHealthSnapshots
  - Id
  - ModelId
  - CapturedUtc
  - PassRate
  - TotalChecks
  - PassedChecks
  - FailedChecks
  - ReportOnlyChecks
  - Warnings
  - FileSizeMb
  - ModelElements
  - RawJson optional

- FailedChecks
  - Id
  - SnapshotId
  - Name
  - Count
  - Category
  - Priority
  - Discipline

- Metrics
  - Id
  - SnapshotId
  - Name
  - Count

- Categories
  - Id
  - SnapshotId
  - Name
  - Value

- Families
  - Id
  - SnapshotId
  - Name
  - Kb

- PassedChecks
  - Id
  - SnapshotId
  - Name

Repository behavior
- Resolve the model to a stable ModelKey
- Return the latest snapshot for that model
- If no snapshot exists, return a meaningful empty response
- Seed sample data for local dev

Model key rules
Generate a stable ModelKey from:
- cloud models: prefer projectGuid + modelGuid
- non-cloud models: use normalized central path if available, otherwise normalized full path + document title

APS integration requirements
Even though the MVP can work from your own database and local extraction, prepare the backend for APS integration for ACC/Revit Cloud models.

Implement an Autodesk service wrapper with these responsibilities:
- obtain APS OAuth token
- call APS Data Management for item/version metadata
- optionally call ACC linked-files endpoint for published cloud models
- keep all APS details isolated from controllers

Use configuration placeholders for:
- APS client id
- APS client secret
- APS callback url if needed later
- APS scopes

APS auth notes to respect
- Use OAuth bearer tokens
- For project/data reads, request at least data:read
- Structure the code so adding 3-legged auth later is possible
- For server-to-server flows, isolate the token cache

APS calls to support in service layer
Build wrappers for these capabilities, even if some are initially stubbed:

1. Get latest item metadata
Suggested APS route:
GET /data/v1/projects/{project_id}/items/{item_id}
or
GET /data/v1/projects/{project_id}/items/{item_id}/tip

2. Get version metadata
Suggested APS route:
GET /data/v1/projects/{project_id}/versions/{version_id}

3. Get published Revit cloud model host/linked file metadata and signed URLs
Suggested APS route:
GET /construction/rcm/v1/projects/{projectId}/published-versions/{versionId}/linked-files

4. Optional future webhook registration support
Suggested APS route:
POST /webhooks/v1/systems/{system}/events/{event}/hooks

Important implementation guidance for APS
- Encapsulate APS routes in a dedicated client/service
- Do not scatter raw APS URLs across the codebase
- Use typed DTOs
- Add defensive handling for missing GUIDs, unpublished models, or unsupported versions
- If a cloud model has no published version available, return a graceful message in the dashboard instead of failing

Local extraction support
Create a clean seam for future local Revit extraction logic.
Add an interface such as:
IModelHealthExtractionService

This interface should allow a future implementation that reads:
- warnings
- failed checks
- family sizes
- category counts
- pass rate
- custom QA rules

For this MVP:
- provide a mock implementation returning seeded/sample data
- optionally create a placeholder Revit-based implementation with TODO comments

ViewModel requirements
The main ViewModel must expose:
- ModelName
- ProjectName
- LastRefreshedText
- IsLoading
- HasData
- ErrorMessage
- Summary
- FailedChecks collection
- Metrics collection
- Categories collection
- Families collection
- PassedChecks collection
- LoadCommand
- RefreshCommand

It must also:
- manage the timer lifecycle
- prevent reentrancy
- update UI thread safely
- expose friendly status text such as:
  - Ready
  - Loading latest model health data...
  - Last updated 5 minutes ago
  - Unable to load model health data

API client requirements in add-in
Create a dedicated API client class that:
- uses HttpClient
- accepts base URL from config
- has methods:
  - LoadModelHealthAsync(ModelIdentity request)
  - RefreshModelHealthAsync(ModelIdentity request)
- handles timeouts
- deserializes JSON safely
- returns typed responses

Config requirements
Support configuration for:
- API base URL
- refresh interval minutes
- enable auto-refresh true/false
- request timeout seconds
- logging level

Sample default config:
- refresh interval = 5
- auto refresh enabled = true
- timeout = 30 seconds

Logging
Implement structured logging for:
- command launch
- window open/close
- refresh start/end
- API request URL and duration
- exceptions

Do not log secrets.

Error handling
Handle these cases cleanly:
- backend unavailable
- timeout
- empty response
- malformed response
- no snapshot found for this model
- unsupported cloud metadata
- missing model identifiers

Each case must show a friendly message in the WPF UI and log details internally.

Testing
Generate:
- unit tests for ViewModel refresh logic
- tests for timer not overlapping refresh calls
- tests for API client deserialization
- backend service tests for latest snapshot resolution

Implementation style
- clean, readable, production-minded code
- no giant files
- no magic strings
- strong typing
- DTOs and services separated cleanly
- cancellation tokens where appropriate
- comments only where helpful

Deliverables
1. Full folder structure
2. Revit add-in code
3. WPF XAML
4. ViewModels
5. DTOs
6. ASP.NET Core API
7. Database entities and migrations
8. Mock data seeding
9. Config templates
10. README with:
   - how to run backend
   - how to configure API URL
   - how to install the add-in
   - how refresh works
   - where to plug in APS credentials
   - where to add real Revit extraction logic later

Do not return pseudocode only.
Generate actual code scaffolding and implementation for the MVP.

Also include a short “Future Extensions” section in the README covering:
- APS webhooks
- cloud automation
- full local Revit extraction
- history charts
- issue creation
- Power BI export
Exact Revit-year delivery requirements (2026-09-17)
This slice continues the existing Spec.md format; no specification-package migration is required.
The build/packaging contract below supersedes the generic runtime choice in the original MVP brief.

- RY-1: Revit 2024 uses net48 with API 24; 2025 uses net8.0-windows with API 25;
  2026 uses net8.0-windows with API 26; 2027 uses net10.0-windows with API 27.
- RY-2: Each year supports Debug and Release configurations with isolated outputs:
  net48-2024, net8.0-windows-2025, net8.0-windows-2026, net10.0-windows-2027.
- RY-3: Installer, updater, Suite deployment, staging and manual ZIPs must route each
  supported Revit year to that exact year's DLL. Missing artifacts fail packaging.
  Updater validation checks compiled API and runtime metadata and rejects fallback.
- RY-4: Keep updater binaries separate from add-in payloads; ship complete resources
  and dependencies. Install/uninstall only product-owned folders/manifests.
- RY-5: Preserve unrelated working-tree edits. Validate all eight year configurations,
  complete automated suite, updater, scripts/XML, four payloads/ZIPs and installer.
  Do not launch Revit during automated verification.

[x] RY-T1 (RY-1 through RY-5): Implement and verify exact-year delivery. All eight configurations passed; 18 tests passed; updater, package hashes, scripts/XML and installer validated. Manual host/lifecycle checks remain with the maintainer as documented.
Durable contract: README.md, build/Revit.props, build/Revit.targets,
scripts/RevitArtifacts.ps1 and src/Modena.RevitAddin.Updater/RevitDeployment.cs.
Evidence and manual smoke-test scope: docs/revit-build-verification.md.
