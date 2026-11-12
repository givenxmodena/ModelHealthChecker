---
title: ACC cloud integration design
doc_type: spec
artifact_type: design
status: accepted
owner: Pulse maintainers
last_reviewed: 2026-10-27
---

# Design

## Overview

Optional APS enrichment sits beside the existing local extraction pipeline.

## Reconciliation and authority

Already implemented: Revit cloud/source detection, project/model GUIDs, local
health checks, user sign-in and in-memory token cache. Missing: cloud HTTP pipeline,
resolved identity, async cloud sign-in and source/status presentation. The empty
ApsCloudModelService.cs is the integration home. ApsAuthService is legacy 2-legged
authentication and is not suitable for ordinary per-user ACC access. Existing
README backend prose is historical; no backend project is present in this checkout.

## High-Level Design

### Cloud boundary

ICloudModelService returns CloudModelInfo (item/version/view name/object count).
ApsCloudModelService uses the user's token and fixed official HTTPS API origin.
Enumerate accessible hubs to resolve the known project's root, recursively search
tip versions for its exact model GUID, then follow the version's derivative ID to
metadata and properties. Follow pagination only on the same HTTPS API origin,
bound page counts and request time, reject redirects/ambiguous matches, and never
request translations. Known-item refresh reads its latest tip and validates GUIDs.
Do not derive a derivative URN by encoding a Revit model GUID or item lineage ID.

Published property counts are separate cloud context, not local rule results. The
open document can have unpublished edits. Local health scores and historical records
therefore retain their current semantics and exclude tokens/cloud property payloads.

## Low-Level Design

### Authentication and UI

Reuse AuthService's user OAuth flow with a cancellable modeless sign-in path for
cloud access. Bind cache entries to the configured client ID. Validate state and
exact callback URL; do not include raw OAuth error bodies or callback errors in
logs/UI. Use PKCE when no client secret is configured; retain the existing secret
exchange for the installed confidential application. No credential values change.
Skip the command's pre-window auth gate only for cloud models; their optional cloud
access happens after rendering and degrades to local data on failure.

ModelHealthViewModel starts bounded cloud work with its refresh cancellation token
alongside local extraction, marshals status updates through its Dispatcher, and
awaits completion without allowing stale/cancelled results to overwrite a newer
refresh. Silent retries never open a login prompt. Shared header gains source and
small cloud status/identity text; no tabs or existing controls are replaced.

## Operational Considerations

No backend or extra package is deployed. Existing credential/app registration and
ACC permissions are prerequisites. Network/search/response limits and transient
fallback are documented in the durable reference. No new cloud data enters history.

## Open Questions

No blocking design decision. Real user/app/model access remains T003; the current
Revit document is local and cannot establish that acceptance.

## Verification, risk and promotion

Tests use a fake HttpMessageHandler/token provider: exact identity matches, paging,
forbidden/missing derivatives, HTTP 202, malformed payloads, cross-origin next
links, cancellation/timeouts and no raw errors/tokens in output. Dispatcher tests
verify fallback, local success independent of cloud failure and UI identity/status.
Build all four Revit targets. Review security/privacy and integration contracts
against actual diff; record findings and disposition in verification.md. Promote
behavior/setup/failure limits into docs/acc-cloud-integration.md.

Primary API references:
- https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Application_and_Document/Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_CloudFiles_html.html
- https://aps.autodesk.com/blog/search-sample-forge-datamanagement-api
- https://aps.autodesk.com/blog/get-derivative-urn-accbim360-file-viewing-it-viewer
- https://aps.autodesk.com/en/docs/model-derivative/v2/tutorials/xtract-metadata/task4-extract_metadata
- https://autodesk-developer.zendesk.com/hc/en-us/articles/30644763025037-Getting-a-Token-with-PKCE-Desktop-App
