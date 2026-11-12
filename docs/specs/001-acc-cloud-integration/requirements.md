---
title: ACC cloud integration
doc_type: spec
artifact_type: requirements
status: accepted
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# Requirements

## Durable Source Baseline

User authorized MHC-13 implementation using Spec Lifecycle Manager. Source of
acceptance: Jira MHC-13 and the existing code, not the outdated backend sketch in
README/Spec.md. No repository templates or AGENTS.md exist. Docs root is docs/;
use the skill fallback package structure. Preserve all pre-existing working edits.

## Goals

Provide optional, accurately identified published ACC data without losing health
checks on the open model or blocking the dashboard on cloud availability.

## Success Criteria

Automated HTTP/auth/UI checks and all supported Revit builds pass. T003 records
real cloud evidence before MHC-13 acceptance; mocked tests establish code readiness.

### Requirement 1: Read cloud data without losing local health checks

#### Acceptance Criteria

1. WHEN the active model is ACC-hosted, Pulse SHALL resolve its ACC item and
   latest published version using the actual project/model GUIDs and the signed-in
   user's APS Data Management access, then read Model Derivative model properties.
2. Pulse SHALL show published cloud version metadata and object count separately
   from local health results. Local Revit checks SHALL remain the score and element
   navigation source, including when local edits differ from the published model.
3. IF metadata cannot be resolved unambiguously, derivatives are unavailable,
   access is denied or the network fails, Pulse SHALL retain local checks and show
   "Cloud extraction unavailable — using local data" with a concise safe reason.

### Requirement 2: Authenticate after the cloud dashboard opens

#### Acceptance Criteria

1. Pulse SHALL open the cloud dashboard before token acquisition, displaying
   "Authenticating…" without blocking local results on cloud network requests.
2. Cloud access SHALL use user-authorized 3-legged OAuth; fresh user sign-in may
   occur on initial/manual loads, never on recurring silent refreshes. Cancellation
   and timeouts SHALL be bounded, and closing Pulse SHALL cancel its cloud work.
3. Tokens SHALL be cached in memory by client and expiry, never logged or written
   to scan history. OAuth state and exact callback origin/path SHALL be validated.
   Public desktop clients SHALL use PKCE; existing confidential clients retain
   their configured authorization-code exchange.

### Requirement 3: Explain source and identity in the existing UI

#### Acceptance Criteria

1. Pulse SHALL show ACC Cloud, Revit Server or Local in the shared header without
   adding tabs or changing existing Dashboard/History controls.
2. Resolved ACC Item ID and Version URN SHALL be visible with wrapping/tooltips.
   Metadata resolution SHALL not invent IDs or silently select a filename match.
3. Local models SHALL issue no APS cloud requests. Existing history, family loading,
   refresh, cancellation and current authentication gate for local models SHALL
   continue to work on Revit 2024/2025/2026/2027.

## Correctness Properties

- CP-001: Published data never changes local scores, element IDs or scan history.
- CP-002: Only exact model identity and trusted Autodesk URLs receive cloud access.
- CP-003: Cancellation/failure never publishes stale results or discards local data.

## Non-Goals

No new backend, cloud uploads/translations, credential provisioning or cloud-based
replacement for Revit-only rules. No secrets/config changes, Git commits, pushes
or Jira Done transition without subsequent user instruction. Automated integration
and UI tests establish code readiness; a real accessible ACC model/user session is
required before claiming end-to-end cloud acceptance. Retain active spec if absent.

Durable promotion: docs/acc-cloud-integration.md plus a short README current-state
link. Existing History release QA remains separate in MHC-24/T004 of spec 000.
