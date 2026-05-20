using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Extracts model health data directly from the live Revit document.
/// Extraction is split into a fast phase and a slow family-size phase so the UI
/// can render meaningful data without waiting for every family document to open.
/// </summary>
public interface IModelHealthExtractor
{
    /// <summary>Full extraction (fast + families). Kept for backward compatibility.</summary>
    Task<DashboardResponse> ExtractAsync(IRevitDocumentContext context, CancellationToken ct);

    /// <summary>
    /// Fast phase — categories, warnings, and health checks only.
    /// Returns quickly; Families list is empty.
    /// </summary>
    Task<DashboardResponse> ExtractFastAsync(IRevitDocumentContext context, CancellationToken ct);

    /// <summary>
    /// Slow phase — opens each family document to measure its size on disk.
    /// Called after ExtractFastAsync so the dashboard is already visible.
    /// </summary>
    Task<List<FamilyDto>> ExtractFamilySizesAsync(IRevitDocumentContext context, CancellationToken ct);

    /// <summary>
    /// On-demand phase — opens each editable family, saves to a temp file, measures KB, then closes.
    /// Invokes onProgress(current, total) after each family so the caller can update a progress indicator.
    /// Only called when the user explicitly requests sizes; can take several minutes on large models.
    /// </summary>
    Task<List<FamilyDto>> ExtractFamilySizesKbAsync(
        IRevitDocumentContext context,
        Action<int, int>? onProgress,
        CancellationToken ct);
}
