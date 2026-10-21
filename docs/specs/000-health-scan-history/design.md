---
title: Health scan history design
doc_type: spec
artifact_type: design
status: accepted
owner: Pulse maintainers
last_reviewed: 2026-10-19
---

# Technical Design

## Overview

Initial baseline (before T001): History was not implemented. Both scan paths saved
only the latest snapshot. T001–T003 implemented local history and two main tabs.
Both silent and interactive paths call SaveSnapshotAsync
after both extraction phases; cache restore does not. Legacy links all modern C#
and XAML. Supported builds also include Revit 2027/net10, beyond the old proposal.
The original dashboard was one scrolling stack with nested family tabs. Its
subtree remains intact inside Dashboard, alongside History under the shared header.
Worktree already contains unrelated user build/packaging changes; preserve them.
Reviewed again after numbered acceptance criteria were normalized; behavior and
task boundaries remain aligned with Requirements 1–4.

## Requirement Coverage

| Requirement | Design | Validation |
|---|---|---|
| 1 / CP-001 | Compact DTO, append-only service, scan completion hook | Storage and orchestration tests |
| 2 / CP-003 | Two-tab wrapper, isolated HistoryView and native TrendChart | XAML subtree comparison, WPF render and build |
| 3 / CP-002 | Passed names plus failed summaries, HistoryViewModel | Comparison/selection tests |
| 4 | Existing dependencies, worker I/O, virtualized grid, reduced geometry | Four year builds, test suite, render |

## High-Level Design

### Components and data flow

- Shared HealthScanRecord: version 1, unique scan ID, UTC timestamp, model identity,
  metrics/categories and trimmed failed summaries, plus passed check names. Names
  are the existing extractor's check identity; renamed checks are not comparable.
- HealthHistoryService: default `%AppData%/Modena/history/`; sanitized bounded key
  prefix plus SHA256 suffix avoids unsafe paths, long filenames and collisions.
  A named per-path mutex serializes readers/writers across Revit sessions. Read
  line-by-line, skip malformed/unknown-version/wrong-model records, order by UTC.
  Separate an interrupted final line before appending. No pruning or deduplication.
- ModelHealthViewModel: use extractor model key from document context (central
  path for workshared documents), without changing existing cache identity. Build
  record only on completed scans; await worker append and refresh loaded History.
  Expose load/save status separately from dashboard extraction errors.
- HistoryViewModel: load on model initialization and History opening, newest-first
  rows and chronological chart records; version overlapping loads so stale results
  cannot replace newer state. Preserve manually selected scan IDs. Default follows
  previous/latest; best preset follows best/latest. A changed check set is disclosed.
- HistoryView: own scoped styles inheriting window palette/cards; virtualized scan
  grid and comparison grid, selectors, B/C buttons, empty/loading/error states.
- TrendChart: DrawingContext/StreamGeometry, fixed percentage scale, timestamp x
  axis, binary search hover, pixel-bucket extrema for bounded line geometry.

## Low-Level Design

Record projection and evaluated-check membership are pure DTO/view-model logic.
Service append/load owns persistence and locking; view code owns WPF layout only.

## Operational Considerations

### Errors, compatibility and trust

Storage errors are logged and surfaced in History, never reclassified as scan
failure. Cache restoration is not historical evidence. Missing evaluated-check
membership is unknown, not zero. UTC persisted; local date/time displayed. No new
packages/network calls. DTOs target netstandard2.0/net8. File APIs and rendering
remain compatible with net48. Local rename/move can split history; no migration.
No deletion UI. Large history files are retained and loaded on worker threads;
memory still grows with history (accepted v1 limit).

## Open Questions

No blocking implementation decisions remain. Live Revit release checks are T004.

## Verification and promotion

### Comparison usability follow-up (T005)

Requirement 5 is a presentation/view-model change; storage and dashboard stay
unchanged. At widths below 1250, put comparison in detail row 0 and scan history
in row 1; shorten the scan grid to 240px (355px wide). Move Reload history into
the chart heading; collapse empty status/warning text. The chart is 180px high
stacked and 230px wide; same-scan selections share one B/C marker. Keep full role labels and
dated scan selectors in the comparison panel, with full-value tooltips for clipped
labels. Add an explicit selected-pair caption above the result.

Replace the unchanged checkbox with four native keyboard-accessible filter choices:
Changed (default), Worsened, Improved, All. Centralize change grouping on the delta
model so filtering and ordering agree. Newly failing precedes regressed, then
resolved/improved, not comparable and unchanged. The summary sentence counts
changes across all evaluated checks regardless of filter; it never claims count
improvement caused the score to rise. Explain empty filters and changed check sets.
Verify ordering/filter semantics and real WPF layout at normal/minimum/wide sizes,
including selection persistence. Promote updated behavior to the durable reference.
User's initial no-trial test reported that the app looked okay; this is visual
feedback, not completion of the T004 live release checklist. Revit remains open
with that test build, so the follow-up no-trial R26 build uses isolated Release
output rather than overwriting the running Debug binary.

Test storage isolation, duplicate scans, corruption/interrupted tails, concurrent
writes, comparison semantics, zero/one records, selections, and scan success/failure
hooks. Build each supported Revit year. Render actual WPF views with synthetic data
at default/minimum sizes; this does not substitute for live Revit smoke checks.
Review actual code for data/reliability and UI regression risks. Promote lasting
behavior to `docs/health-scan-history.md` and README. Retain the active spec with
truthful outstanding manual checks if live verification is unavailable.
