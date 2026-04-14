namespace Modena.Shared.DTOs;

public class DashboardResponse
{
    public string ModelKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; }
    public SummaryDto Summary { get; set; } = new();
    public List<FailedCheckDto> FailedChecks { get; set; } = new();
    public List<MetricDto> Metrics { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
    public List<FamilyDto> Families { get; set; } = new();
    public List<string> PassedChecks { get; set; } = new();
}
