---
title: Health scan history traceability
doc_type: spec
artifact_type: traceability
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-26
---

# Traceability Matrix

## Task To Context Matrix

| Task ID | Requirements | Acceptance Criteria | Design Sections | Change Impact | Verification | Durable Targets | Open Decisions |
|---|---|---|---|---|---|---|---|
| T008 | Requirement 2 | R2 AC4 | design.md#equal-detail-card-heights-t008 | none | Actual WPF card-height assertions | docs/health-scan-history.md | none |
| T007 | Requirement 7, Requirement 2, Requirement 3 | R7 AC1–AC3 | design.md#scan-history-pagination-t007 | none | Paging and WPF binding tests | docs/health-scan-history.md | none |
| T006 | Requirement 6, Requirement 2 | R6 AC1–AC4 | design.md#summary-tile-follow-up-t006 | none | Focused tile tests and WPF renders | docs/health-scan-history.md | none |
| T001 | Requirement 1, Requirement 3, Requirement 4 | R1 AC1–AC3; R3 AC1–AC3; R4 AC1–AC2 | design.md#components-and-data-flow; design.md#errors-compatibility-and-trust | none | Storage, comparison and orchestration tests | docs/health-scan-history.md | none |
| T002 | Requirement 2, Requirement 3, Requirement 4 | R2 AC1–AC3; R3 AC1–AC2; R4 AC1–AC2 | design.md#components-and-data-flow | none | Four builds, actual WPF renders, dashboard subtree comparison | docs/health-scan-history.md | none |
| T003 | Requirement 1, Requirement 2, Requirement 3, Requirement 4 | All acceptance criteria | design.md#verification-and-promotion | none | verification.md | docs/health-scan-history.md, README.md | none |
| T004 | Requirement 1, Requirement 2, Requirement 3, Requirement 4 | Live release smoke | design.md#verification-and-promotion | none | verification.md#manual-or-external-verification | docs/health-scan-history.md | none |
| T005 | Requirement 5, Requirement 2, Requirement 3 | R5 AC1, AC2, AC3 | design.md#comparison-usability-follow-up-t005 | none | verification.md#comparison-usability-follow-up | docs/health-scan-history.md | none |

## Correctness Property Coverage

| Property | Requirements | Tasks | Verification |
|---|---|---|---|
| CP-001 | Requirement 1 | T001, T003 | Storage and successful/failed/cached scan tests |
| CP-002 | Requirement 3 | T001, T003 | Missing-check comparison tests |
| CP-003 | Requirement 2 | T002, T003 | Original dashboard subtree equivalence |

## Requirement To Delivery Matrix

| Requirement | Priority | Acceptance Criteria | Design Sections | Tasks | Verification | Durable Targets | Coverage State | Residual Destination |
|---|---|---|---|---|---|---|---|---|
| Requirement 1 | must-have | AC1, AC2, AC3 | design.md#components-and-data-flow | T001, T003 | verification.md#requirement-coverage | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 2 | must-have | AC1, AC2, AC3 | design.md#components-and-data-flow | T002, T003 | verification.md#requirement-coverage | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 3 | must-have | AC1, AC2, AC3 | design.md#components-and-data-flow | T001, T002, T003 | verification.md#requirement-coverage | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 4 | must-have | AC1, AC2 | design.md#errors-compatibility-and-trust | T001, T002, T003 | verification.md#requirement-coverage | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 5 | must-have | AC1, AC2, AC3 | design.md#comparison-usability-follow-up-t005 | T005 | verification.md#comparison-usability-follow-up | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 6 | must-have | AC1, AC2, AC3, AC4 | design.md#summary-tile-follow-up-t006 | T006 | verification.md#summary-tile-follow-up | docs/health-scan-history.md | complete | T004 live release smoke |
| Requirement 7 | must-have | AC1, AC2, AC3 | design.md#scan-history-pagination-t007 | T007 | verification.md#scan-history-pagination | docs/health-scan-history.md | complete | T004 live release smoke |

## Design To Implementation Matrix

| Design Section | Requirements | Tasks | Interfaces Or Files | Verification | Coverage State | Residual Destination |
|---|---|---|---|---|---|---|
| Components and data flow | Requirement 1, Requirement 2, Requirement 3 | T001, T002 | HealthScanRecord, HealthHistoryService, HistoryViewModel, HistoryView, TrendChart, ModelHealthWindow | verification.md | complete | none |
| Errors, compatibility and trust | Requirement 1, Requirement 4 | T001, T003 | HealthHistoryService, ModelHealthViewModel | verification.md | complete | none |
| Summary tile follow-up (T006) | Requirement 6, Requirement 2 | T006 | HistoryView, HistoryViewModel, DeltaBrushConverter | verification.md#summary-tile-follow-up | complete | T004 live release smoke |
| Scan history pagination (T007) | Requirement 7, Requirement 2, Requirement 3 | T007 | HistoryViewModel, HistoryView | verification.md#scan-history-pagination | complete | T004 live release smoke |

## Open Decision Impact

No unresolved product decisions. Numbered criteria were normalized without changing
behavior; downstream design/tasks and this matrix were reviewed against them.

No blocking product decisions remain. Live Revit smoke verification is tracked
separately from automated checks; its outcome will be recorded without inference.
## Jira story delivery mapping

| Story | Delivery scope | Spec tasks | Commit |
|---|---|---|---|
| MHC-20 | persist completed health scans | T001 | `a7beacfe` |
| MHC-21 | add the History tab beside Dashboard | T002 | `f64962bf` |
| MHC-22 | add scan history rows and selection | T001, T002, T005 | `def0ee0d` |
| MHC-14 | plot scan pass rates over time | T002 | `d3f24ef0` |
| MHC-23 | summarize scan history | T001, T002 | `e34cd8af` |
| MHC-19 | compare scans and triage check changes | T001, T002, T005 | `f48e34fb` |

Shared documentation and verification are maintained by T003. MHC-24 (QA task)
and T004 live release verification remain separate from implementation completion.
