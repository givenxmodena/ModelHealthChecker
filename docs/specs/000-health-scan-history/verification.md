---
title: Health scan history verification
doc_type: spec
artifact_type: verification
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-21
---

# Verification

## Scope

Requirements 1–5, tasks T001–T003/T005 and CP-001–CP-003. User changes existing before
this branch are preserved. Feature commits are recorded below; no push or release
packaging is included. Source/test/build evidence is separate from live Revit.

## Validation Commands

| Command | Result | Evidence |
|---|---|---|
| `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj --filter FullyQualifiedName~HealthHistory -nowarn:MSB3277` | pass | 13 tests in final full run, including real WPF binding/selection/filter/render test |
| `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj -nowarn:MSB3277` | pass | 31 passed, 0 failed, 0 skipped; includes existing property and artifact tests |
| `dotnet build src/Modena.RevitAddin.Legacy/Modena.RevitAddin.Legacy.csproj -c 'Debug R24' -p:Platform=x64` | pass | net48; existing ElementId deprecation/nullability warnings |
| `dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Debug R25' -p:Platform=x64` | pass | net8, Revit 2025 |
| Same modern command with `Debug R26` | pass | net8, Revit 2026 |
| Same modern command with `Debug R27` | pass | net10, Revit 2027 |
| `dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R26' -p:Platform=x64 -p:DisableTrialVersion=true` | pass | Isolated no-trial test build; does not overwrite running Debug build |
| XML subtree comparison against pre-change working-tree XAML | pass | All dashboard state/content subtrees equal; exactly Dashboard/History main tabs |

MSB3277 assembly-version warnings from installed Revit references are suppressed
in routine test/build output only; no project warning policy was changed.

## Quality Gates

| Gate | Status | Evidence |
|---|---|---|
| Requirements/design/task traceability | pass | Numbered criteria and CP-001–CP-003 mapped below and in traceability.md |
| Automated tests and WPF renders | pass | 31 tests; actual WPF output inspected, selection/filter/stacked layout verified |
| Durable promotion | pass | Reference and README updated; proposal marked historical |
| Live Revit release acceptance | outstanding | T004; package remains active |
| Spec cleanup | retain active | Implementation commit recorded below; no closure/cleanup archive claimed |

## Requirement Coverage

| Requirement | Acceptance criteria covered | Evidence | Residual risk |
|---|---|---|---|
| Requirement 1 | AC1, AC2, AC3 | HealthHistoryTests: completed/silent/cached/failed/cancelled scans; compact DTOs; ordering/duplicates; concurrent writers; malformed tails, isolation, schema filtering, write failures | Real multi-process Revit session smoke remains |
| Requirement 2 | AC1, AC2, AC3 | Original dashboard subtree comparison; HealthHistoryViewTests real WPF render at 780/1080/1580 widths, empty state and binding trace; source review of timestamp scale/hover | Live mouse/DPI/trial checks remain |
| Requirement 3 | AC1, AC2, AC3 | HealthHistoryTests: all delta types, missing-check semantics, zero/one scans, presets/manual persistence; WPF selectors survive bound ItemsSource replacement | Live keyboard/dropdown interactions remain |
| Requirement 4 | AC1, AC2 | Four target builds; no package edits; Task.Run I/O; grid virtualization and bounded extrema geometry source review | Unbounded retained history still consumes memory |
| Requirement 5 | AC1, AC2, AC3 | T005 comparison ordering/filter/empty-summary tests; WPF assertions for filter binding, dated labels, selection persistence, stacked row order and reduced table height | New layout not tested live in Revit yet |

## Correctness Property Coverage

| Property | Covered by | Evidence | Residual risk |
|---|---|---|---|
| CP-001 | T001, T003 | HealthHistoryTests retained duplicates/order/corruption and full scan lifecycle hook test | Disk failure can prevent saving; warning is shown |
| CP-002 | T001, T003 | Missing-check and changed-status comparison assertions | Check renames remain not comparable |
| CP-003 | T002, T003 | All original dashboard content subtrees compare equal against pre-feature working-tree snapshot | Header is now fixed above tab content; live dashboard smoke pending |

## Expert Review

Agent performed an explicit role-based review of actual source and test artifacts;
this is not an independent second-reviewer approval.

- Software/data architecture: single compact contract/service/view model, existing
  dependencies, model-key reuse, UTC persistence, deterministic previous/best presets.
- Reliability/privacy: per-path mutex, bounded filename plus hash, corrupt-line
  recovery, no element IDs or network transmission, separate history failures.
- QA/UI: original dashboard subtree preserved; actual WPF selectors/rows/chart
  rendered. Found and fixed selected-item display and selection-reset problems.
- Performance: replaced repeated best-scan sorting with one linear selection;
  chart retains bucket extrema; rows virtualized. Full retained records remain in
  memory by accepted v1 design.

## Durable Promotion And Cleanup

Behavior, storage/recovery and limits promoted to `docs/health-scan-history.md` and
linked from README. Original proposal marked superseded. The package is retained
active pending live Revit verification; implementation is committed, with no spec closure/archive claimed.
Canonical-context artifact is unnecessary: user screenshot governs appearance,
current code governs contracts, proposal is explicitly historical; no unresolved
source authority conflict remains.

## Manual Or External Verification

Live Revit smoke has not been performed. Pulse maintainers own the release checks
listed in `docs/health-scan-history.md#validation-and-remaining-limits`. This remains
a release validation item; automated rendering is not recorded as live acceptance.

## Evidence Log

2026-10-19: branch created; scan/cache and UI baseline reconciled; focused/full tests,
four API-targeted builds, XML subtree equality and offscreen WPF renders checked.
No policy conflict or unresolved implementation review finding identified.
Final rerun after source/UI fixes: all 29 tests and all four target builds pass.
Repository validation and whitespace checks pass. Package lint has zero errors;
its EARS wording and optional canonical-context warnings are advisory. Concrete
acceptance criteria are parsed/covered; source-authority rationale is recorded above.

## Residual Risks

Live Revit, DPI, trial and multi-process checks remain T004. Unlimited history
grows on disk/in memory. Renamed local models/checks do not have a migration path.

## Comparison usability follow-up

T005 implements Requirement 5 using the existing History view/model and theme.
Comparison now precedes scan history below 1250px width; chart/table heights reduce
to 180/240px. Wide layout remains 230/355px, side by side. Empty status text takes
no space, Reload history is in the chart heading, and quick presets share the
comparison heading. Full selection roles, dated captions and full-value tooltips
clarify which pair is being compared. Same-scan chart labels share a B/C marker.

Tests verify worsened-first ordering, four filters, unknown-check accounting,
summary grammar, same/empty check sets, filter persistence and selected scan
stability. Actual WPF renders at 780, 1080 and 1580 widths were inspected, including
the normal 1080x740 viewport, no-worsened and empty-history states. No binding errors.
An initial offscreen assertion incorrectly used IsVisible on an unshown Window;
it now checks Visibility and arranged height, matching this rendering method.

Final verification: 31 tests pass; all four Debug API targets and Release R26
no-trial compile successfully. Legacy retains its 8 pre-existing warnings. Repository
validation and original dashboard subtree/two-tab checks pass. Role-based review
confirmed storage and Dashboard are unchanged; summary/filter logic shares one
delta model, and no production dependency or trial-default setting was changed.

User reported the original no-trial app looked okay after launch. That is bounded
visual feedback only; T004 remains open. Revit is running the earlier Debug no-trial
build, so this follow-up was built separately without replacing loaded binaries.

## Story commit preparation (2026-10-21)

The original bundled implementation and documentation commits were split locally
at the user's request. The prior tip is retained on
`codex/backup-health-history-before-story-split`. No push, Jira transition, release
acceptance or spec closure is implied.

| Story | Commit | Scope |
|---|---|---|
| MHC-20 | `a7beacfee7d93ae34f9e16672bd7eb2e4170b308` | persist completed health scans |
| MHC-21 | `f64962bfd1a1fb0bde6c9aaefa50514eaebd2ed1` | add the History tab beside Dashboard |
| MHC-22 | `def0ee0d2352b54eeab9ef655d47fdcd7fdeae9c` | add scan history rows and selection |
| MHC-14 | `d3f24ef08fa9fddba89b177cb63613f943c6a9f9` | plot scan pass rates over time |
| MHC-23 | `e34cd8af83c2059fa10604ce3de061ace2d396d8` | summarize scan history |
| MHC-19 | `f48e34fb3713925a77da1f121551b9abf321d389` | compare scans and triage check changes |

Each story snapshot was exported with `git archive` into an isolated temporary
directory and checked with:

```powershell
dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj -p:DisableTrialVersion=true --verbosity minimal
```

MHC-20, MHC-21, MHC-22, MHC-14 and MHC-23 each pass 15 tests; MHC-19 passes
the complete committed suite of 20 tests, including actual WPF layout and binding
checks. No tests failed or were skipped. The existing Revit reference assembly
conflict warnings remain. These checks use the committed base, excluding unrelated
working-tree build/packaging, authentication and family-extraction changes.

The final source and tests are byte-for-byte identical in Git to the original
implementation tree (`8bd13dc`); only commit boundaries and this documentation
record changed. The shared documentation follows in its own commit. `git diff
--check` passes across the complete split. An intermediate scan-list XAML nesting
error was corrected before validation and the branch update.

The earlier 31-test/four-year results describe the broader working tree, including
pre-existing build infrastructure and artifact tests excluded from these feature
commits. README (including its History link), build documentation and unrelated
working edits remain uncommitted. T004 live Revit release smoke remains open.

The story commits form an ordered dependency chain on `b1766b7`. Four pre-existing
local commits between `origin/main` and that base are outside this split; a normal
push of the full branch would include those ancestors too. Push selection remains
with the user.
