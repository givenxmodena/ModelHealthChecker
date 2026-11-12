---
title: ACC integration traceability
doc_type: spec
artifact_type: traceability
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# Traceability

## Task To Context Matrix

| Task ID | Requirements | Acceptance Criteria | Design Sections | Change Impact | Verification | Durable Targets | Open Decisions |
|---|---|---|---|---|---|---|---|
| T001 | Requirement 1, Requirement 2, Requirement 3 | R1 AC1–AC3; R2 AC1–AC3; R3 AC1–AC3 | design.md#cloud-boundary; design.md#authentication-and-ui | MHC-13 | Fake HTTP, dispatcher tests, four builds, security/integration review | docs/acc-cloud-integration.md | none |
| T002 | Requirement 1, Requirement 2, Requirement 3 | Durable contract and operational evidence | design.md#verification-risk-and-promotion | README historical context | verification.md | docs/acc-cloud-integration.md, README.md | none |
| T003 | Requirement 1, Requirement 2, Requirement 3 | Live ACC acceptance | design.md#verification-risk-and-promotion | MHC-13 closure | verification.md | docs/acc-cloud-integration.md | Accessible ACC user/model required |

## Requirement To Delivery Matrix

| Requirement | Priority | Acceptance Criteria | Design Sections | Tasks | Verification | Durable Targets | Coverage State | Residual Destination |
|---|---|---|---|---|---|---|---|---|
| Requirement 1 | must-have | AC1–AC3 | design.md#cloud-boundary | T001, T002, T003 | verification.md#evidence-log | docs/acc-cloud-integration.md | implemented, automated checks pass | T003 live ACC |
| Requirement 2 | must-have | AC1–AC3 | design.md#authentication-and-ui | T001, T002, T003 | verification.md#evidence-log | docs/acc-cloud-integration.md | implemented, automated checks pass | T003 live OAuth |
| Requirement 3 | must-have | AC1–AC3 | design.md#authentication-and-ui | T001, T002, T003 | verification.md#evidence-log | docs/acc-cloud-integration.md | implemented, automated checks pass | T003 live UI |

## Design To Implementation Matrix

| Design Section | Requirements | Tasks | Interfaces Or Files | Verification | Coverage State | Residual Destination |
|---|---|---|---|---|---|---|
| Cloud boundary | Requirement 1 | T001 | ICloudModelService, ApsCloudModelService | CloudIntegrationTests | implemented | T003 |
| Authentication and UI | Requirement 2, Requirement 3 | T001 | AuthService, AuthWindow, ModelHealthViewModel, ModelHealthWindow, RevitContextService | CloudIntegrationTests, existing regression tests | implemented | T003 |
| Verification, risk and promotion | Requirement 1, Requirement 2, Requirement 3 | T002, T003 | Durable reference, README, verification.md | Recorded automated and review evidence | promoted | T003 |

## Open Decision Impact

No unresolved code/design decisions. Live ACC access is an acceptance prerequisite,
owned by Pulse maintainers in T003; it is not inferred from local sample tests.
