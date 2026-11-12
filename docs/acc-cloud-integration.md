---
title: ACC cloud integration
doc_type: reference
status: implemented-pending-live-validation
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# ACC cloud integration

Pulse reads the open Revit document for health checks and optionally enriches ACC
models with published cloud information. The shared header identifies ACC Cloud,
Revit Server (RSN paths) or Local. File-based worksharing is Local. No extra tab is
added. Source of truth: RevitContextService, ApsCloudModelService, AuthService and
ModelHealthViewModel; update this document with behavioral changes.

## Cloud flow

The dashboard opens before cloud authentication. Local checks can render while
the header reports "Authenticating…" and then "Loading published cloud data…".
Using the signed-in user's access, Pulse finds the ACC project root, searches
published tip versions for the exact project/model GUIDs, and reads the selected
version's Model Derivative property view. It shows the ACC Item ID, published
Version URN, view name and property-object count. Later refreshes query the known
item's tip and revalidate its model GUID. No uploads/translations are requested.

Published cloud data may lag current Revit edits. Its object count is separate
from local element totals and never changes health pass rates, warnings, element
selection or historical scan data. Item/Version identifiers belong to the
resolved ModelIdentity; synchronous RevitDocumentContext getters cannot resolve
APS-only IDs and are not used as a substitute for the network lookup.

## Authentication and setup

- Configure the existing APS application/client through the existing credential
  provider or PluginConfig. This feature does not write credentials or create a
  backend. The client's Autodesk registration must permit the chosen auth flow
  and the registered redirect URI `http://localhost:8080/`.
- Cloud requests use a user-authorized 3-legged token with `data:read` and
  `viewables:read` scopes (plus the existing `user-profile:read` sign-in scope).
  The user and application must have access to the relevant ACC project and files.
- Public clients without a secret use S256 PKCE. Existing confidential clients
  retain authorization-code exchange with their configured secret. Provisioning
  or migrating an Autodesk application is an administrator action outside this fix.
- Initial/manual cloud loads may show a cancellable modeless sign-in window.
  Recurring silent refreshes never open one. Access tokens are kept in memory
  for the client until shortly before expiry. Once expired, use Refresh to sign
  in again; no refresh tokens or access tokens are persisted by this flow.
- Existing required sign-in for local models remains in place. Cloud-model
  sign-in failures degrade to local checks so the dashboard remains usable.

## Failure and resource limits

Cloud failures show "Cloud extraction unavailable — using local data" with a
safe reason. Denied access, unpublished/missing/processing derivatives, ambiguous
model matches, malformed responses and network failures do not invalidate local
health scans. Resolved item/version identifiers remain visible when derivative
properties are unavailable. No filename-only match is accepted.

Cloud requests use the official HTTPS Autodesk API origin, disallow redirects and
reject cross-origin pagination links. Search is limited to 50 pages/20,000 rows;
each response is limited to 20 MiB. The configured request timeout bounds the
cloud network phase, excluding time spent signing in. Closing Pulse cancels both
the network work and sign-in; cancelled/older requests cannot publish new UI data.
Logs and UI omit raw authentication/API bodies and tokens. Local models make no
cloud API requests. Existing credential-storage policy is unchanged.

## Validation status

Mocked HTTP, OAuth callback, dispatcher and WPF tests cover the integration and
local fallback. Live ACC acceptance still requires an accessible cloud model and
its Autodesk user/app permissions. A local sample model cannot establish this.
The active package is docs/specs/001-acc-cloud-integration; T003 remains the real
cloud smoke check. MHC-13 must not be marked Done solely from mocked tests.

API contract references: [cloud model identity](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Application_and_Document/Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_CloudFiles_html.html),
[recursive version search](https://aps.autodesk.com/blog/search-sample-forge-datamanagement-api),
[derivative URNs](https://aps.autodesk.com/blog/get-derivative-urn-accbim360-file-viewing-it-viewer),
[property extraction](https://aps.autodesk.com/en/docs/model-derivative/v2/tutorials/xtract-metadata/task4-extract_metadata).
