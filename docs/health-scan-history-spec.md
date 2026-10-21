# Health Scan History — Feature Spec

Status: superseded proposal. Implementation and current behavior are documented in
[Health scan history](health-scan-history.md); delivery evidence is tracked in
[the active lifecycle package](specs/000-health-scan-history/verification.md).
The design below is historical context, not the current source of truth.

## Goal

Keep a record of past health scans per model so users can see whether the model's
health is improving or regressing over time, and compare any two scans to see
which specific checks got better or worse.

## Current architecture (relevant pieces)

- `RevitHealthExtractor.cs` (`src/Modena.RevitAddin/Services/RevitHealthExtractor.cs`)
  runs all checks and assembles a `DashboardResponse` via `BuildResponse()` (~line 134).
  `BuildModelKey()` (~line 812) already derives a stable per-model key: cloud models use
  `Slugify(ProjectGuid-ModelGuid)`, local models use `Slugify(CentralModelPath/ModelPath + DocumentTitle)`.
- `ModelHealthViewModel.FetchDataAsync()` (`src/Modena.RevitAddin/ViewModels/ModelHealthViewModel.cs`,
  ~line 312) orchestrates each scan and calls a `SaveSnapshot()` step (~line 665) afterward —
  this is the hook point for also appending to history.
- `DashboardCacheService` (`src/Modena.RevitAddin/Services/DashboardCacheService.cs`) is the existing
  precedent for local persistence: one JSON file per model at `%AppData%\Modena\cache\{modelKey}.json`,
  written with Newtonsoft.Json, **overwritten** each scan (no history kept today).
- `DonutChart.cs` (`src/Modena.RevitAddin/Views/DonutChart.cs`) is the precedent for chart rendering:
  a hand-rolled `FrameworkElement` subclass drawing directly via `DrawingContext`/`PathGeometry`/`ArcSegment`
  in `OnRender()`. No charting NuGet package exists anywhere in the solution.
- Two build targets to keep compatible: `Modena.RevitAddin` (.NET 8) and `Modena.RevitAddin.Legacy` (net48).
- No existing History/Trend/Compare UI or persistence beyond the single-snapshot cache.

## Design decisions (already made — do not re-litigate without reason)

1. **Logging scope**: append to history on *every* successful scan, not just explicit
   user-triggered ones, and no de-duping.
2. **UI surface**: a dedicated **History tab**, not inline deltas on the existing dashboard.
3. **Retention**: no pruning for now.
4. **Storage format**: JSON Lines (`.jsonl`), one file per model, via Newtonsoft.Json
   (already a dependency). Rejected SQLite/LiteDB — unnecessary dependency for this data
   volume, and would need vetting against the net48 Legacy build.
5. **Chart rendering**: hand-rolled native WPF `FrameworkElement`, following the `DonutChart.cs`
   pattern — not a charting NuGet package. (If hand-rolling proves too costly, OxyPlot is the
   fallback candidate since it targets netstandard2.0 and works on both net48 and net8.)

## New components to build

1. **`HealthScanRecord` DTO** — `src/Modena.Shared/DTOs/HealthScanRecord.cs`
   Compact, diffable record per scan: `Timestamp` (UTC), `ModelKey`, `ModelName`, `ProjectName`,
   `PassRate`, `TotalChecks`, `PassedChecks`, `FailedChecks`, `ReportOnlyChecks`, `WarningsCount`,
   `FileSizeMb`, `ModelElements`, `Categories` (name/count pairs), and a trimmed
   `FailedCheckSummaries` list (`Name`, `Count`, `Category`, `Priority` only — no `ElementIds`,
   `Description`, or `Recommendation`, to keep records small).

2. **`HealthHistoryService`** — `src/Modena.RevitAddin/Services/HealthHistoryService.cs`
   Mirrors `DashboardCacheService`'s style:
   - `Append(modelKey, HealthScanRecord)` — appends one JSON line to
     `%AppData%\Modena\history\{sanitized-modelKey}.jsonl`.
   - `LoadHistory(modelKey)` — reads all lines, deserializes to `List<HealthScanRecord>`
     ordered by timestamp.

3. **Hook in `ModelHealthViewModel.FetchDataAsync`** — after the existing `SaveSnapshot()` call,
   build a `HealthScanRecord` from the just-computed `DashboardResponse` and call
   `HealthHistoryService.Append()`.

4. **`HistoryViewModel`** (new) — loads history via `HealthHistoryService.LoadHistory(modelKey)`
   when the History tab opens or the model loads. Exposes: the scan list, summary stats
   (scans logged, latest pass rate + delta vs previous, best scan, trend direction), and
   baseline/current selection state for the comparison panel, with per-check deltas computed
   from `FailedCheckSummaries`.

5. **`TrendChart`** (new) — `src/Modena.RevitAddin/Views/TrendChart.cs`, a
   `FrameworkElement` rendering pass-rate-over-time as a line + area fill + grid via
   `DrawingContext`/`StreamGeometry`, following `DonutChart.cs`'s structure. Needs:
   hover tooltip (nearest point by x-position via `OnMouseMove`), and two marker
   highlights for whichever scans are selected as baseline/current in the comparison panel.

6. **New History tab** in `ModelHealthWindow.xaml`, containing:
   - Summary stat row (scans logged, latest pass rate w/ delta chip, best scan, trend direction)
   - The `TrendChart` (pass rate over time, full scan history)
   - A scan list table (date, pass rate, passed/total, warnings, elements, delta vs previous)
   - A comparison panel: baseline/current scan selectors, a pass-rate delta headline, and a
     per-check delta list (then → now, with a colored improved/regressed/no-change chip)

## Visual reference

A clickable HTML mockup was built during design discussion to visualize the tab's layout,
matching Pulse's actual dark theme (colors sampled from the real dashboard: background
`#0a0e17`, accent blue `#3e7bfa`, good green `#24c78b`, bad red `#f0475f`, warn amber `#efa23c`).
It demonstrates the trend chart with hover tooltips and a live baseline/current comparison.
Ask the user for the artifact link if it's needed as a reference — it isn't saved in this repo.

## Open items to resolve during implementation

- Confirm how tab/section navigation actually works in `ModelHealthWindow.xaml` today
  (the mockup assumed a simple top tab bar; verify against the real XAML — it's possible
  navigation is instead driven by the icon row visible in the bottom-left of the window).
- Decide whether `Modena.RevitAddin.Legacy` (net48) needs the History tab too, or whether
  that build is extraction-only.
- Decide the comparison panel's default selection — the mockup defaults to best-scan vs.
  latest; latest-vs-immediately-previous may be a better default, with best-scan as a
  manual alternative.

## Key files to touch

- `src/Modena.Shared/DTOs/HealthScanRecord.cs` (new)
- `src/Modena.RevitAddin/Services/HealthHistoryService.cs` (new)
- `src/Modena.RevitAddin/Services/DashboardCacheService.cs` (pattern reference, not modified)
- `src/Modena.RevitAddin/ViewModels/ModelHealthViewModel.cs` (hook into `FetchDataAsync`)
- `src/Modena.RevitAddin/ViewModels/HistoryViewModel.cs` (new)
- `src/Modena.RevitAddin/Views/ModelHealthWindow.xaml` (new History tab)
- `src/Modena.RevitAddin/Views/DonutChart.cs` (pattern reference, not modified)
- `src/Modena.RevitAddin/Views/TrendChart.cs` (new)
