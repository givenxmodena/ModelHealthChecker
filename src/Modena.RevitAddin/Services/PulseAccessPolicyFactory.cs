#if TRIAL_VERSION
using MLBS.Desktop.Client;
using System.Reflection;

namespace Modena.RevitAddin.Services;

public static class PulseAccessPolicyFactory
{
    public static IPulseAccessPolicy Create()
    {
        var productCode = typeof(PulseAccessPolicyFactory).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "PulseMlbsProductCode").Value!;
        var compiledApiUrl = typeof(PulseAccessPolicyFactory).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "PulseMlbsApiBaseUrl").Value!;
        var apiUri = new Uri((Environment.GetEnvironmentVariable("PULSE_MLBS_API_URL")
            ?? compiledApiUrl).TrimEnd('/') + "/");
        LogService.Info($"MLBS configured: Product=Pulse, ProductCode={productCode}, API={apiUri.GetLeftPart(UriPartial.Path)}");
        return new PulseAccessPolicy(new MlbsDesktopClient(new MlbsClientOptions
        {
            ApiBaseUri = apiUri,
            ProductCode = productCode,
            ProductDisplayName = "Pulse",
            RequestTimeout = TimeSpan.FromSeconds(30),
            LocalStateKey = "Pulse"
        }), new RegistryTrialState());
    }

    private sealed class RegistryTrialState : IPulseTrialState
    {
        public PulseTrialInfo GetStatus()
        {
            var status = TrialManager.GetTrialStatus();
            return new PulseTrialInfo
            {
                IsActive = status.IsTrialActive,
                EndDate = status.TrialEndDate,
                RefreshesUsed = status.TotalRefreshes,
                Message = status.GetStatusMessage()
            };
        }
        public void RecordRefresh() => TrialManager.RecordRefresh();
    }
}
#endif
