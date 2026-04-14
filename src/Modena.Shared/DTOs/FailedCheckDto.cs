namespace Modena.Shared.DTOs;

public class FailedCheckDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
}
