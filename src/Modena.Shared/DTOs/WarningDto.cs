namespace Modena.Shared.DTOs;

public class WarningDto
{
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ElementCount { get; set; }

    /// <summary>IDs of the elements attached to this warning group. Used by Show in Model.</summary>
    public List<long> ElementIds { get; set; } = new();

    public bool CanNavigate => ElementIds.Count > 0;
}
