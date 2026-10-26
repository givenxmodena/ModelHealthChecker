---
title: Health scan history verification
doc_type: spec
artifact_type: verification
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-26
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

## Summary tile follow-up

2026-10-26, T006 / Requirement 6: Refined the four History tiles to follow the
user's reference with compact navy cards, outline icons, an inline signed badge
and dated comparison captions. Latest direction is calculated from the previous
chronological scan independently of the selected comparison pair and peak score.
Below-best and at-best states have their own dated captions; zero/one-scan states
do not show a previous-scan badge. Existing stored scores and Dashboard are unchanged.

- `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj -p:DisableTrialVersion=true --verbosity quiet`:
  39 passed, zero failures/skips. Includes seven numeric scenarios (up/down/flat,
  recovery below best, tied peak and 0/100 boundaries), zero/one-scan states,
  notification/selection independence and actual WPF badge colors/bindings.
- Actual WPF renders inspected at 780, 1080 and 1580 layout units, including a
  recovery with green +10 pp and a simultaneous red -10 pp below-best value.
  No binding errors. Default/minimum tiles fit; empty state hides the badge.
- No-trial `dotnet build` for `Debug R24` (Legacy) and `Debug R25`, `Debug R26`,
  `Debug R27` (modern), with `-p:Platform=x64 -p:DisableTrialVersion=true
  -p:DeployToRevit=false`: all pass, zero errors. Existing reference conflict
  warnings remain (8 on Legacy, 2 on each modern target).
- Focused diff whitespace check passes. Review confirms no new dependency,
  persistence change, tab change or Dashboard edit. Added only tile presentation,
  derived labels, reusable delta badge backgrounds and focused tests.

Changes remain uncommitted and unpushed. After the user closed Revit and requested
a test launch, the Release R26 build with `DisableTrialVersion=true` succeeded
(2 existing warnings, zero errors). The standalone manifest was verified to point
to `bin/Release R26/notrial/net8.0-windows-2026/Modena.RevitAddin.dll`, and Revit
2026 was launched. User testing is pending; this does not establish live release
acceptance. T004 remains open. Lifecycle lint: zero errors, 2 existing advisories.

## Scan history pagination

2026-10-26, T007 / Requirement 7: Scan History now binds to a 10-row page slice
with Previous/Next controls, page count and record-range text. Full Scans and
Records remain the sources for comparison selectors, chart, summary metrics and
chronological deltas. Paging preserves selections. Refresh follows latest on
page 1 and retains a page containing the former first-visible record on older
pages; model changes reset to page 1. Navigation clamps at valid boundaries.
Table scrolling resets independently without moving the surrounding page.

- `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj
  -p:DisableTrialVersion=true --verbosity quiet`: 45 passed, zero failures/skips.
  Paging tests cover 0/1/10/11/25 records, complete traversal, guarded boundaries,
  chronological deltas at page boundaries, cross-page selection, inserting 12
  new scans while browsing older history, following latest, model switches and
  empty state. WPF tests verify button enabled states, bound row/page counts,
  all-history selectors and stable outer scroll position across navigation.
- Actual WPF stacked/narrow/wide page renders inspected at 780/1080/1580 layout
  units; pager labels/buttons fit and binding error collection remains empty.
- Final-source no-trial Debug R24/R25/R26/R27 builds all pass, zero errors,
  with the existing 8 Legacy/2-per-modern reference conflict warnings.
- Focused diff whitespace checks pass. No storage, score calculation,
  Dashboard, main-tab or production-dependency change. Durable reference updated.

A separate user-authorized live model repair pinned six unpinned Revit links.
Pulse's existing Refresh command was invoked; the persisted successful scan
reported 47% with 7/15 checks passing and the link-pinning check passing. These
model edits are in the open document; saving was not performed. This bounded
check does not complete T004's full live release checklist. Pagination remains
uncommitted/unpushed. On the user's subsequent build/launch request, no Revit
instance remained open. Release R26 with `DisableTrialVersion=true` built
successfully (2 existing warnings, zero errors), the standalone manifest was
verified against the rebuilt DLL, and Revit 2026 was launched for pagination
testing. User acceptance of the pager remains pending.

## Equal detail-card heights

2026-10-26, T008 / Requirement 2 AC4: Changed only the two detail Borders from
Top to Stretch alignment. The existing shared auto row now gives Scan History
and Compare Scans equal card heights; separate stacked rows retain natural
heights. Table caps, scrolling, pagination and comparison content stay intact.

The existing `HealthHistory_ActualWpfViewsRenderAndKeepBoundSelections` test
passes with actual-height equality assertions for wide cards both with check
rows and with no changed checks. Actual stacked/minimum/wide renders and pager
bindings also pass with zero binding errors. Wide render inspected: aligned
bottom edges. Command: `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj
--filter FullyQualifiedName~HealthHistory_ActualWpfViewsRenderAndKeepBoundSelections
-p:DisableTrialVersion=true --verbosity quiet` (1 passed, zero failures/skips;
existing reference conflict warnings). Changes remain local; the running Revit
session has not been restarted for this adjustment. T004 remains open.

## Refinement commit preparation

2026-10-26: User authorized committing completed History refinements.

| Scope | Commit | Isolated committed-source verification |
|---|---|---|
| tiles | `0bbb33a29a0cb73dbc7f8c9d04145de83b180b2f` | Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, |
| pagination | `0bf764fd8492baa1dc85de75a24a275956f9b055` | Passed!  - Failed:     0, Passed:    34, Skipped:     0, Total:    34, |
| card-heights | `3720a6053acb42835c1ad07f11a41c53bf8666c9` | Passed!  - Failed:     0, Passed:    34, Skipped:     0, Total:    34, |

Each exact staged tree was exported using `git archive` and passed the full
committed test suite with `dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj
-p:DisableTrialVersion=true --verbosity quiet`. This excludes unrelated working-tree
build/packaging, authentication, family-extraction and artifact-test changes.
The broader working-tree suite remains 45 tests. Card-height assertions were
verified in the final snapshot. All feature source/test files match the reviewed
working versions, and unrelated tracked/untracked edits were preserved.

MHC-19 and MHC-22 were moved to Done in Jira at the user's request; the other four
History stories were already Done. MHC-13 remains To Do: cloud detection and
local checks exist, but APS Model Derivative extraction and authentication/fallback
presentation remain absent, and ACC ItemId/VersionUrn getters still return null.
MHC-24 and T004 remain outstanding for full release QA. No push is authorized by
this commit-preparation request.
