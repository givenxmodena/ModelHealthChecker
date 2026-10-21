---
title: Health scan history
doc_type: spec
artifact_type: requirements
status: accepted
owner: Pulse maintainers
last_reviewed: 2026-10-19
---

# Requirements

## Introduction

Record model health across successful scans and explain changes between two scans.
User approved implementation on 2026-10-19, with exactly two main tabs and the
existing dashboard appearance preserved. The supplied current-app screenshot is
the visual authority; the generated mockup illustrates History interactions only.

## Durable Source Baseline

The existing `docs/health-scan-history-spec.md` is a proposal, not implemented
behavior. `ModelHealthViewModel`, `DashboardCacheService`, `RevitHealthExtractor`
and `ModelHealthWindow.xaml` are the current behavior baseline. README build
instructions cover Revit 2024–2027. No repository spec templates or AGENTS.md exist;
use the skill fallback requirements/design/tasks format, scaled to this feature.

## Goals

Track health changes without altering the existing dashboard or adding dependencies.

## Success Criteria

Automated storage/comparison/UI checks and four supported Revit builds pass; live
Revit smoke checks are reported separately.

## Requirements

### Requirement 1: Record successful scans

**Priority:** must-have

Users need an honest history of actual scans.

#### Acceptance Criteria

1. WHEN an interactive or automatic scan completes, THE SYSTEM SHALL append one compact
  UTC record per model; cache restores, failed/cancelled scans and family-size-only
  requests append nothing. Identical results are retained, without pruning.
2. Records retain summary metrics, categories, failed-check counts and evaluated
  passed check names, without element IDs, descriptions or recommendations.
3. History is local per Windows user; concurrent appends do not interleave,
  malformed lines do not hide valid records, and persistence errors do not fail scans.

### Requirement 2: Preserve the dashboard and add History

**Priority:** must-have

Users retain their familiar dashboard.

#### Acceptance Criteria

1. Exactly two main tabs exist: Dashboard (default) and History. Existing
  dashboard content, styles, refresh, trial banner and family sub-tabs remain intact.
2. History uses existing palette/cards/fonts, shows scan count, latest rate and
  previous delta, best scan and change since best; handles zero/one records honestly.
3. A native WPF chart uses a fixed 0–100% scale, timestamp-based x positions,
  nearest-point hover details and matching baseline/current markers. Scan rows show
  local date/time, rate, passed/total, warnings, elements and previous delta.

### Requirement 3: Explain comparisons

**Priority:** must-have

Users can select any baseline/current pair.

#### Acceptance Criteria

1. Default is previous to latest; presets support previous/latest and best/latest.
  B/C row buttons and selectors update matching chart/row highlights.
2. Comparisons show percentage-point delta and per-check affected-item counts,
  newly failing, resolved, improved, regressed and unchanged states; changed/all filters
  and a coverage summary account for evaluated checks.
3. A missing check is not assumed to pass; checks evaluated in only one scan
  are marked not comparable. Manual selections survive subsequent scans.

### Requirement 4: Keep implementation lean and compatible

**Priority:** must-have



#### Acceptance Criteria

1. No new production dependencies; JSON Lines uses Newtonsoft.Json and charts
  use WPF drawing. Shared UI compiles for Revit 2024/net48, 2025–26/net8 and 2027/net10.
2. File I/O runs off the UI thread; scan rows are virtualized and chart geometry
  is bounded by viewport resolution while hover uses the full scan history.

### Requirement 5: Make comparisons easier to use

**Priority:** must-have

User approved this follow-up after testing the initial no-trial build.

#### Acceptance Criteria

1. At narrow/default widths, comparison appears immediately after the chart and
   before scan history; the scan table is shorter and empty toolbar space is removed.
   Wide layout remains side by side, using existing theme and exactly two main tabs.
2. Baseline/current selections display full role labels, local dates/times and rates,
   linked to the existing B/C row shortcuts and chart markers. Selection persistence
   and previous/best presets continue to work.
3. Newly failing/regressed checks appear before resolved/improved checks. Changed,
   Worsened, Improved and All filters work independently of scan selection; a concise
   factual sentence explains changes, with honest no-change/unknown/empty states.

## Correctness Properties

- CP-001: History never invents a scan or deletes an earlier valid scan.
- CP-002: Comparisons only infer pass/fail when a check was evaluated in both scans.
- CP-003: Dashboard content remains unchanged inside its new default tab.

## Non-Goals

Cloud/team sharing, API changes, retention settings, exporting, historical element
navigation, repairing old files and changing model identity rules are out of scope.

## Durable Impact

Promote behavior/storage/recovery limitations to `docs/health-scan-history.md` and
link from README. Mark the initial proposal superseded after implementation.

## Staged Readiness

Requirements accepted from user instructions and proposal. No design-first
exception. Next: design/tasks reconciliation, implementation, verification.
