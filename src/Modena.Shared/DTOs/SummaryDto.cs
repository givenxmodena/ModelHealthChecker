namespace Modena.Shared.DTOs;

public class SummaryDto
{
    public int PassRate { get; set; }
    public int TotalChecks { get; set; }
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
    public int ReportOnlyChecks { get; set; }
    public int Warnings { get; set; }
    public int FileSizeMb { get; set; }
    public int ModelElements { get; set; }
}
