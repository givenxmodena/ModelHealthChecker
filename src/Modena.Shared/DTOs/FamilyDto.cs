namespace Modena.Shared.DTOs;

public class FamilyDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>File size in KB. Only populated when the source .rfa path is known; otherwise 0.</summary>
    public int Kb { get; set; }

    /// <summary>Number of instances placed in the model. Primary sort and display metric.</summary>
    public int InstanceCount { get; set; }
}
