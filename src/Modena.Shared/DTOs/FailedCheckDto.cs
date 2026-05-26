namespace Modena.Shared.DTOs;

public class FailedCheckDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;

    /// <summary>IDs of the elements that caused this check to fail. Used by Show in Model.</summary>
    public List<long> ElementIds { get; set; } = new();

    /// <summary>True when there are elements to navigate to in the model.</summary>
    public bool CanNavigate => ElementIds.Count > 0;
}
