using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Extracts model health data directly from the live Revit document.
/// Replaces the backend API call — extraction happens in-process.
/// </summary>
public interface IModelHealthExtractor
{
    Task<DashboardResponse> ExtractAsync(IRevitDocumentContext context, CancellationToken ct);
}
