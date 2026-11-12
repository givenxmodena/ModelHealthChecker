# Pulse trial and MLBS access

Pulse retains its existing 60-day / 1,000-successful-refresh trial. A valid stored
MLBS identity selects paid access before consulting the local trial. Otherwise an
independently active trial can continue. When the trial expires, opening Pulse
requires successful MLBS activation. An expired trial no longer prevents the
ribbon button from being registered at Revit startup.

The dashboard shows **Sign In / Activate Licence** during the trial and
**Licence / Profile** after activation. Activation switches the open dashboard to
paid mode. Paid refreshes do not consume the local trial allowance. Sign-out
removes paid access; only an independently active trial can then authorize work.
Refreshes and on-demand family-size extraction check access before extraction.
Automatic refresh does not open licensing dialogs and stops when access is denied.
Failed or cancelled refreshes do not increment trial usage.

## Product identity

Pulse uses `50554C5345`, the UTF-8 hexadecimal encoding of uppercase `PULSE`.
This replaced the temporary Exportly code after Chris provisioned PULSE on
staging. Product resolution and both wallet balances were verified against
staging on 12 November 2026. The UI and local session namespace are `Pulse`;
Exportly's saved local session is not reused or overwritten.

`PulseMlbsProductCode` is defined in `build/Mlbs.Desktop.Client.targets`.
For an individual controlled build, MSBuild also accepts:

```powershell
dotnet build src/Modena.RevitAddin/Modena.RevitAddin.csproj -c 'Release R26' -p:Platform=x64 -p:PulseMlbsProductCode=<registered-hex-code>
```

The product identity is assembly metadata, not an installed setting that can
disable licensing. `PULSE_MLBS_API_URL` can select a test service; the default is
`https://mms.modena-app.co.za/mlbsapi/`. The existing `DisableTrialVersion=true`
test-build mechanism still omits production trial/licensing composition; do not
use it for customer releases.

## Shared client and backend behavior

The desktop client timeout is 30 seconds. Both Pulse and Exportly explicitly
request 30 seconds, and the supplied Modern/Legacy MLBS client bundles use the
same default. Restart a desktop host after replacing its DLL; an already loaded
library keeps its old behavior until the process exits. Requests are not
automatically retried, because a timed-out sign-in can still consume a token.

The reference implementation is Exportly in `One Sync/OneSync`, specifically
`MlbsReleaseAccessPolicy` and `TrialOrMlbsReleaseAccessPolicy`. The reusable client
source is `MLBS-exportly-client-poc/MLBS.Desktop.Client`, not the older
`MLBS/RevitAddinCore` Revit sample. See `libs/MLBS.Desktop.Client/README.md` for
bundle provenance and rebuilding.

Pulse uses `ValidateSigninAsync` for paid checks, matching Exportly's operation
authorization, and does not call `RecordUsageAsync` after model refreshes.
**This does not mean validation is free of backend token activity:** the local
MLBS API implementation's sign-in endpoint can consume one subscription token
depending on the configured subscription type. Paid command initialization,
refreshes (including automatic refresh), and family-size authorization all
revalidate through sign-in. Confirm this server behavior and the intended billing
policy before customer rollout. A staging sign-in test with the old debug identity
returned `REGISTRATION_REQUIRED`; the balances remained unchanged. Successful
registration/sign-in remains a user test, and no registration was submitted by
the test harness.

MLBS sign-in/registration is separate from Pulse's existing optional Autodesk
authentication and ACC access. Local trial registry data is neither reset nor
migrated by activation. Trial accounting uses the access mode selected at the
start of the scan, so activation during a pending trial scan cannot erase its usage.

## Verification

Run the Pulse tests, the shared client tests for both runtimes, and all four
year-specific builds. Repository syntax validation is `scripts/Validate-Repository.ps1`.
`scripts/RevitArtifacts.ps1` requires the MLBS client and runtime dependencies in
each staged payload. Standard package scripts copy the complete payload folders.

Live Revit smoke remains required: active trial, expiry-to-activation, cancellation,
registration, profile/sign-out, service outage with active/expired trials, paid
refresh and family extraction, and reopening Pulse after restarting each supported
Revit year. Use a controlled MLBS account because sign-in may affect tokens.

## Staging test launch

```powershell
./scripts/Test-MlbsStaging.ps1 -ReportPath logs/mlbs-staging-readonly.json
./build-and-launch-2026.ps1 -Launch -StagingTest -MlbsApiUrl https://mmsstaging.modena-app.co.za/mlbsapi/
```

The launch builds into an isolated `mlbs-staging` Debug output and points the
2026 development manifest there. The staging URL is also embedded as assembly
metadata in this isolated build, so restarting Revit outside the launch script
does not silently switch this build to production. Its output-only config disables automatic refresh
and the unrelated Autodesk gate for local-model licensing tests; production config
and Release output are unchanged. The launcher also passes the URL to the new Revit
process without saving a machine/user environment variable. Close Revit
before running the launch script. Run it without `-StagingTest` to restore the
normal Debug manifest.

The allocated client address confirmed by the balance endpoints is
`rBsNGrUnSzaCgBi3cnjMvACTzyiEpiXc5r`. Before user registration it held 10 LIC:PULSE
and 10 PPS:PULSE tokens. Enter this **client** address with your own email in
**Create account**; the API creates/resolves the user identity. Never enter a
private key or seed.
