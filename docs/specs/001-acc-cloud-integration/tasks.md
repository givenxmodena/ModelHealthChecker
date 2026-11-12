---
title: ACC cloud integration tasks
doc_type: spec
artifact_type: tasks
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# Tasks

- [x] T001 Implement cloud access, async authentication and safe local fallback.
  - Depends on: none
  - Requirements: Requirement 1 AC1–AC3; Requirement 2 AC1–AC3; Requirement 3 AC1–AC3.
  - Design: Cloud boundary; Authentication and UI.
  - Files: ApsCloudModelService.cs, AuthService.cs, AuthWindow.xaml.cs, ModelHealthCheckerCommand.cs, ModelHealthViewModel.cs, ModelHealthWindow.xaml, focused cloud tests.
  - Acceptance: Exact cloud identity/properties resolved with bounded user-authorized requests; status and identity displayed; failures preserve local results; no token leakage.
  - Validation: Fake HTTP and dispatcher tests; existing add-in regression suite; four Revit target builds; security/integration diff review.
  - Evidence: 79 working-tree tests pass (34 cloud/source cases), including mocked APS retrieval/fallback, OAuth callbacks/PKCE, silent suppression, local score preservation, close cancellation and WPF labels. Final-source Revit 2024/2025/2026/2027 no-trial Debug builds pass with existing warnings. Security/integration self-review findings resolved in verification.md. Live ACC acceptance remains T003.
  - Evidence mode: command

- [x] T002 Promote operational documentation and reconcile verification.
  - Depends on: T001
  - Requirements: Requirement 1, Requirement 2, Requirement 3.
  - Design: Verification, risk and promotion.
  - Files: docs/acc-cloud-integration.md, README.md, verification.md.
  - Acceptance: Durable contract, setup/access requirements and limitations match the code; all automated evidence and review dispositions recorded.
  - Validation: Documentation/code review and lifecycle lint.
  - Evidence: Promoted actual cloud flow, setup/scopes, identity matching, published-vs-local semantics, error/resource/security limits and live-validation boundary to docs/acc-cloud-integration.md. README current-state note reconciled. Actual diff/security/integration and documentation self-review recorded in verification.md; T003 live evidence remains pending.

  - Evidence mode: artifact
- [ ] T003 Verify real ACC access and fallback in Revit.
  - Depends on: T002
  - Requirements: Requirement 1, Requirement 2, Requirement 3.
  - Design: Verification, risk and promotion.
  - Decision owner: Pulse maintainers
  - Files: verification.md.
  - Acceptance: A real accessible ACC model resolves exact identity/derivatives with the configured user/app, and denial/offline/cancellation preserve local checks.
  - Validation: Bounded live Revit and ACC smoke test; do not infer completion from mocked HTTP.
  - Evidence: Requires an accessible ACC model and interactive Autodesk user session.
