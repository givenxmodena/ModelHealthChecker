---
title: ACC cloud integration verification
doc_type: spec
artifact_type: verification
status: active
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# Verification

## Quality Gates

MHC-13 code is ready for live validation, not cloud release acceptance. Existing
build/packaging, credential lookup, family-warning and other working changes were
preserved. No credentials/configuration were read into evidence or modified. No
commit, push, deployment, restart or Jira Done transition was performed.

## Evidence Log

`dotnet test tests/Modena.RevitAddin.Tests/Modena.RevitAddin.Tests.csproj
-p:DisableTrialVersion=true --verbosity quiet`: **79 passed, 0 failed, 0 skipped**.
This is the broader current working tree (45 existing and 34 cloud/source tests).
No inference is made about an isolated committed snapshot; the change is uncommitted.

Cloud tests cover exact model GUID matching, paging, actual derivative IDs and
property collection counts, known-item tip refresh/revalidation, missing GUIDs,
ambiguous matches, HTTP 401/403/404/429/500, HTTP 202 pending derivatives, malformed
property JSON with retained identity, timeouts/cancellation and safe error messages.
Untrusted pagination origins/schemes/ports/paths are rejected before sending tokens.
OAuth tests cover state mismatch/missing/duplicates, callback origin/port/path,
authorization errors, public-client PKCE and silent sign-in suppression. Dispatcher
tests verify local pass rates on cloud success/failure, local-model zero cloud calls,
discarding late results after closing, and actual WPF header/source/identity labels
at minimum width. Source tests distinguish RSN paths from file-based worksharing.

Final-source builds, all with `-p:Platform=x64 -p:DisableTrialVersion=true
-p:DeployToRevit=false --verbosity quiet`:

| Project / configuration | Result |
|---|---|
| Modena.RevitAddin.Legacy / Debug R24 | pass, 0 errors, 8 existing warnings |
| Modena.RevitAddin / Debug R25 | pass, 0 errors, 2 existing warnings |
| Modena.RevitAddin / Debug R26 | pass, 0 errors, 2 existing warnings |
| Modena.RevitAddin / Debug R27 | pass, 0 errors, 2 existing warnings |

Focused diff whitespace check passes. No new production package dependency.

## Role-based implementation review

Reviewer: Codex, inspecting the actual diff in security/privacy, integration and
QA roles; this is a self-review, not an independent audit.

| Finding | Disposition |
|---|---|
| Published ACC properties can lag the open model | Cloud count/version are labelled separately; local checks remain score/navigation/history source |
| Current sign-in opened before cloud dashboard | Cloud pre-window gate bypassed; bounded async cloud path starts after window render; local gate retained |
| Callback prefix/state and raw auth bodies were unsafe | Exact callback/state/duplicate checks added; raw auth/API bodies omitted; redirects disabled |
| Desktop public client cannot protect a secret | No-secret flow uses S256 PKCE; configured confidential flow remains compatible; no credential provisioning added |
| External pagination could expose bearer tokens | Fixed HTTPS API origin/path checks, redirect rejection, response/page/row limits and cancellation |
| Repeated background prompts interrupt users | Silent refresh never shows sign-in; expired token requires manual Refresh |
| Model close could publish stale results | Shared cancellation checked before applying status/identity, with late-result dispatcher test |
| Derivative failure could hide already-resolved identity | Safe typed failure retains item/version for missing, processing or malformed derivative properties |
| Existing central-path inequality mislabelled file worksharing | RSN detection now distinguishes actual Revit Server from local/UNC file worksharing |

No unresolved code-level blocking finding was identified in this review.

## Requirement coverage and durable promotion

| Requirement | Evidence | Remaining acceptance |
|---|---|---|
| Requirement 1 AC1–AC3 | Mocked APS identity/properties/failure tests and local-score dispatcher test | T003 real ACC item/version/derivative retrieval |
| Requirement 2 AC1–AC3 | Callback/PKCE/silent/cancellation tests and WPF status binding | T003 real user/app OAuth acceptance |
| Requirement 3 AC1–AC3 | Source/header/local-regression tests; four Revit builds | T003 live cloud UI and local fallback |

Promoted to docs/acc-cloud-integration.md. README opening now identifies current
add-in behavior and marks absent-backend prose as historical. API sources and
resource limits are recorded in the durable reference and design.

## Residual Risks

`get_running_instances` reported only the local Snowdon sample in Revit 2026.
No accessible ACC document was open. Real app registration/redirect URI, account
permissions, published derivative availability and geographic/project edge cases
cannot be established by mocked HTTP or that local sample. T003 remains pending
with Pulse maintainers; MHC-13 must remain open until that evidence exists.
Existing History release QA (MHC-24/spec 000 T004) is separate.
