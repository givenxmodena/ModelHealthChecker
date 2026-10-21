---
title: Health scan history tasks
doc_type: spec
artifact_type: tasks
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-19
---

# Tasks

Input: requirements.md and design.md in this package. User authorized implementation.
Durable targets: `docs/health-scan-history.md`, README, initial proposal status.

- [x] T001 Persist completed scans and implement history comparison state.
  - Depends on: none
  - Requirements: Requirement 1 AC1–AC3, Requirement 3 AC1–AC3, Requirement 4 AC1–AC2; CP-001, CP-002.
  - Design: Components and data flow; Errors, compatibility and trust.
  - Files: Shared DTO, HealthHistoryService, HistoryViewModel, ModelHealthViewModel, focused tests.
  - Acceptance: Append/load and comparison logic satisfy criteria, with focused tests passing.
  - Validation: dotnet test tests/Modena.RevitAddin.Tests --filter FullyQualifiedName~HealthHistory
  - Evidence: 10 HealthHistory tests pass: append ordering/duplicates, unsafe keys, concurrent writers, corrupt tails, version filtering, compact DTOs, comparison and selection behavior, successful interactive/silent and failed/cancelled scan hooks. Debug R26 compiles (existing MSB3277 warnings).

  - Evidence mode: validation


- [x] T002 Add the History UI and native trend chart while preserving Dashboard.
  - Depends on: T001
  - Requirements: Requirement 2 AC1–AC3, Requirement 3 AC1–AC2, Requirement 4 AC1–AC2; CP-003.
  - Design: Components and data flow.
  - Files: ModelHealthWindow.xaml, HistoryView.xaml/.cs, TrendChart.cs.
  - Acceptance: Two main tabs; original Dashboard subtree retained; actual WPF renders without binding/layout failures.
  - Validation: Four supported year builds; WPF render inspection and subtree comparison.
  - Evidence: Actual WPF views render at 780/1080/1580 widths with zero binding errors; selection labels and persistence verified. Original dashboard content subtrees match the pre-change working-tree snapshot. Four supported Revit targets compiled; final source build sweep recorded in verification.md.

  - Evidence mode: validation
- [x] T003 Verify, review and promote durable documentation.
  - Depends on: T002
  - Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4.
  - Design: Verification and promotion.
  - Files: focused tests, docs/health-scan-history.md, README, verification.md.
  - Acceptance: Automated checks and code review recorded; manual limits explicit; durable behavior documented.
  - Validation: Complete add-in tests, builds and lifecycle checks; manual Revit smoke remains explicit if unavailable.
  - Evidence: Final full suite: 29 passed, 0 failed/skipped. Debug R24/R25/R26/R27 build successfully (8 existing Legacy warnings, no errors). Repository validation and dashboard subtree comparison pass. WPF renders inspected; role-based review and durable promotion recorded in verification.md. Lifecycle lint has 0 errors, 2 advisory warnings; live Revit verification remains T004.
  - Evidence mode: validation

- [ ] T004 Perform live Revit release smoke checks.
  - Depends on: T003
  - Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4.
  - Design: Verification and promotion.
  - Decision owner: Pulse maintainers
  - Files: verification.md, docs/health-scan-history.md.
  - Acceptance: Record real Revit results for initial/cached load, manual/automatic refresh, closing during extraction, two sessions writing one model, trial banner, hover/keyboard selectors, DPI, Dashboard Show actions and family controls.
  - Validation: Manual checklist in docs/health-scan-history.md#validation-and-remaining-limits.
  - Evidence: Not performed; offscreen WPF rendering and API-targeted builds do not establish live Revit behavior. Required before release/spec closure.

- [x] T005 Improve comparison placement, selection clarity and change triage.
  - Depends on: T003
  - Requirements: Requirement 5 AC1, AC2, AC3; Requirement 2; Requirement 3; CP-002, CP-003.
  - Design: Comparison usability follow-up (T005).
  - Files: HistoryView.xaml/.cs, HistoryViewModel.cs, TrendChart.cs, HealthHistoryTests.cs, HealthHistoryViewTests.cs, docs/health-scan-history.md.
  - Acceptance: Comparison precedes scan history in stacked layout; explicit dated selection labels, worsened-first ordering, four filters, factual summary and compact spacing work without changing Dashboard or storage.
  - Validation: Focused comparison tests, actual WPF layout/binding/render checks, full suite, four supported Revit builds including no-trial R26.
  - Evidence: 31 tests pass, including comparison sorting/filter/summary/empty-check cases and real WPF selection/filter/layout checks. Four Debug Revit targets and isolated Release R26 no-trial build pass. Default/minimum/wide renders inspected with zero binding errors; original dashboard subtree and two tabs unchanged. Durable reference and verification evidence updated; T004 live release smoke remains open.
  - Evidence mode: validation
