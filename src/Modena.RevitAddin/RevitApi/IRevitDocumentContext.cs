namespace Modena.RevitAddin.RevitApi;

/// <summary>
/// Abstraction over the active Revit document for extracting model identity.
/// In production, this wraps Autodesk.Revit.DB.Document and UIApplication.
/// </summary>
public interface IRevitDocumentContext
{
    bool HasActiveDocument { get; }
    string? DocumentTitle { get; }
    string? ModelPath { get; }
    string? CentralModelPath { get; }
    string? RevitVersion { get; }
    bool IsCloudModel { get; }
    string? ProjectGuid { get; }
    string? ModelGuid { get; }
    bool IsDocumentSaved { get; }

    /// <summary>
    /// The ACC item ID for cloud models, if available.
    /// </summary>
    string? ItemId { get; }

    /// <summary>
    /// The ACC version URN for cloud models, if available.
    /// </summary>
    string? VersionUrn { get; }

    /// <summary>
    /// Selects the specified elements and zooms the active Revit view to them.
    /// Throws if no view is currently open or the elements cannot be shown.
    /// </summary>
    void ShowElements(IReadOnlyList<long> elementIds);
}
