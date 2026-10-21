namespace Modena.Shared.DTOs;

/// <summary>A compact historical scan. Never contains Revit element IDs.</summary>
public class HealthScanRecord
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; }
    public string ModelKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int PassRate { get; set; }
    public int TotalChecks { get; set; }
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
    public int ReportOnlyChecks { get; set; }
    public int WarningsCount { get; set; }
    public int FileSizeMb { get; set; }
    public int ModelElements { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
    public List<HealthCheckSummary> FailedCheckSummaries { get; set; } = new();
    // Absence from FailedCheckSummaries alone does not establish that a check passed.
    public List<string> PassedCheckNames { get; set; } = new();

    public static HealthScanRecord FromResponse(DashboardResponse response) => new()
    {
        Timestamp = response.LastUpdatedUtc,
        ModelKey = response.ModelKey,
        ModelName = response.ModelName,
        ProjectName = response.ProjectName,
        PassRate = response.Summary.PassRate,
        TotalChecks = response.Summary.TotalChecks,
        PassedChecks = response.Summary.PassedChecks,
        FailedChecks = response.Summary.FailedChecks,
        ReportOnlyChecks = response.Summary.ReportOnlyChecks,
        WarningsCount = response.Summary.Warnings,
        FileSizeMb = response.Summary.FileSizeMb,
        ModelElements = response.Summary.ModelElements,
        Categories = response.Categories.Select(c => new CategoryDto { Name = c.Name, Value = c.Value }).ToList(),
        PassedCheckNames = response.PassedChecks.ToList(),
        FailedCheckSummaries = response.FailedChecks.Select(c => new HealthCheckSummary
        {
            Name = c.Name, Count = c.Count, Category = c.Category, Priority = c.Priority
        }).ToList()
    };
}

public class HealthCheckSummary
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
