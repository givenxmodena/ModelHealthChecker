using Modena.Shared.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Modena.Shared.DTOs;

public class ModelIdentity
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public string RevitVersion { get; set; } = string.Empty;
    public bool IsCloudModel { get; set; }
    public string? ProjectGuid { get; set; }
    public string? ModelGuid { get; set; }

    /// <summary>
    /// The ACC item ID for cloud models. Used to call APS Data Management API.
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// The ACC version URN for cloud models. Used to call APS Model Derivative API.
    /// </summary>
    public string? VersionUrn { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ModelSource ModelSource { get; set; }
}
