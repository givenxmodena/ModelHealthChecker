# PULSE staging API verification — 12 November 2026

## Exportly Suite test-copy update

The Suite review session was using an older Exportly MLBS DLL with a 15-second
default and a production endpoint fallback. The actual module under
`%LOCALAPPDATA%/Modena/Review/20261112-updates-simple/native-session/runtime/Runtime/2026/Modules/Exportly`
has now been updated with a Revit 2026 MLBS Release build. Its compiled service
address is staging, its product code remains Exportly's `4558504F52544C59`, and
its effective client timeout is 30 seconds. Normal Exportly builds keep the
production default; `ExportlyMlbsApiBaseUrl` supplies controlled build overrides.

A reflection probe instantiated the deployed adapter's client without submitting
a sign-in and verified all three settings. All copied files were hash-checked,
and the previous files were backed up under the review root's
`backups/exportly-staging-30s-20261112-153304` directory. Six Exportly licensing
tests passed. Revit was reopened using desktop control after shell launching was
blocked by the tool policy. No new ledger/sign-in request was submitted by this
deployment. User sign-in in the updated Suite is the remaining live check.

API base: `https://mmsstaging.modena-app.co.za/mlbsapi/`.
Product: `50554C5345`, UTF-8 hex for `PULSE`.
Client address: `rBsNGrUnSzaCgBi3cnjMvACTzyiEpiXc5r` (from the previous local MLBS debug session).

## Live results

Nine read-only/invalid-request probes passed:

| Check | Result |
| --- | --- |
| Product resolution | HTTP 200, `TOKEN_CODES_RESOLVED`, success=true |
| Licence balance | HTTP 200, LIC:PULSE balance 10, limit 10 |
| Subscription balance | HTTP 200, PPS:PULSE balance 10, limit 10 |
| Malformed product code | HTTP 400 |
| Missing balance address | HTTP 400 |
| Incomplete sign-in | HTTP 400, `INVALID_REQUEST` |
| Invalid sign-in email | HTTP 400, `INVALID_EMAIL` |
| Incomplete registration | HTTP 400, `INVALID_REQUEST` |
| Invalid registration email | HTTP 400, `INVALID_EMAIL` |

One sign-in with the old debug identity (`user1@mail.com`) returned HTTP 404,
`REGISTRATION_REQUIRED`. This did not establish successful activation. Both
balances remained 10 afterwards. No registration or direct token-consumption
request was submitted. The user will register their own email through Pulse.

Raw selected-field reports are local-only under `logs/mlbs-staging*-2026-11-12.json`.
Re-run read-only checks with `scripts/Test-MlbsStaging.ps1`. Its optional
`-TestSignIn -Email ...` performs one sign-in attempt and records before/after
balances; successful sign-in can consume a subscription token.

## Contract assessment and remaining checks

The tested routes, product-code mapping, `UserEmail` request field, balance
response fields and registration-required status match the desktop client.
The available evidence supports connectivity, configured PULSE tokens and correct
input rejection. It does not yet establish successful registration, successful
paid sign-in, recovery from a ledger outage or charging behavior.

Two backend behaviors need attention during the user test:

- The checked-in API source consumes licence/subscription tokens during initial
  registration and may consume subscription tokens on successful sign-in. Pulse
  uses sign-in for paid refresh authorization, so an automatic refresh can also
  invoke that path. The isolated staging build disables automatic refresh.
- The checked-in registration source commits entitlement rows before confirming
  ledger transfers. Chris should review failure/retry consistency: a failed
  transfer may leave partial registration state. This is a source-review finding,
  not a failure reproduced against staging.

## Revit handoff

Use the **Modena** tab's Pulse button for the standalone test build. The installed
**Modena Suite** tab currently points at an older Pulse payload and is not the
staging deployment. The isolated build is version 1.0.5 and uses PULSE's code.
Verify its log contains `API=https://mmsstaging.modena-app.co.za/mlbsapi/` before
registering. If Revit asks about the unsigned Pulse add-in, the user must decide
whether to load it.

On the expired-trial screen choose **Sign in with an existing licence**, then
**Create account**. Enter your own email and the client address above, register,
then sign in. Compare profile status and both balances before and after. A
successful registration/sign-in test remains pending user interaction.
