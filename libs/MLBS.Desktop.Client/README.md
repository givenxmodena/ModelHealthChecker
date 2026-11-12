# Supplied MLBS desktop client

Pulse consumes the same `MLBS.Desktop.Client` boundary as Exportly, with separate
`Modern` (.NET 8; Revit 2025–2027) and `Legacy` (.NET Framework 4.8; Revit 2024)
bundles. The library owns sign-in, registration, protected session storage, API
calls and profile UI. Pulse owns its trial and the thin access policy adapter.

This bundle is built from the sibling `MLBS-exportly-client-poc` checkout's
`MLBS.Desktop.Client` project. That source was updated for product-specific dialog
labels and a backward-compatible `UsageUsed` / `UsageLabel` trial summary. Existing
Exportly callers using `ExportsUsed` keep their original export-count wording.
The original `MLBS/RevitAddinCore` project is a Revit-hosted sample, not this library.

Refresh these supplied binaries from the MLBS source checkout:

```powershell
./scripts/Update-MlbsClient.ps1 -SourceRoot '../MLBS-exportly-client-poc'
dotnet test '../MLBS-exportly-client-poc/MLBS.Desktop.Client.Tests/MLBS.Desktop.Client.Tests.csproj' -c Release
```

`bundle-hashes.json` records SHA-256 hashes of the supplied binaries. Keep the
source changes and matching bundles together when handing off this integration.
The .NET 8 bundle uses host-framework JSON/HTTP assemblies and supplies ProtectedData;
the .NET 4.8 bundle also supplies the JSON/HTTP compatibility dependencies.
