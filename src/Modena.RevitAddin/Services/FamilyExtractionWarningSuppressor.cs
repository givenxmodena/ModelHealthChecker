using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Silently removes non-critical warnings raised while temporary family documents are
/// opened and saved. The subscription is deliberately scoped to family extraction so
/// warnings from ordinary user operations are never affected.
/// </summary>
internal sealed class FamilyExtractionWarningSuppressor : IDisposable
{
    private readonly Application _application;
    private readonly Dictionary<string, int> _warnings = new(StringComparer.Ordinal);
    private bool _disposed;

    public FamilyExtractionWarningSuppressor(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _application.FailuresProcessing += OnFailuresProcessing;
    }

    public int WarningCount => _warnings.Values.Sum();

    public string BuildSummary() => string.Join(
        "; ",
        _warnings.OrderByDescending(w => w.Value).ThenBy(w => w.Key)
            .Select(w => $"{w.Value}x {w.Key}"));

    private void OnFailuresProcessing(object? sender, FailuresProcessingEventArgs e)
    {
        var accessor = e.GetFailuresAccessor();

        foreach (var failure in accessor.GetFailureMessages())
        {
            // Never suppress errors: only Revit warnings are non-critical for extraction.
            if (failure.GetSeverity() != FailureSeverity.Warning)
                continue;

            var description = failure.GetDescriptionText();
            if (string.IsNullOrWhiteSpace(description))
                description = "Revit warning";

            _warnings[description] = _warnings.TryGetValue(description, out var count)
                ? count + 1
                : 1;

            accessor.DeleteWarning(failure);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _application.FailuresProcessing -= OnFailuresProcessing;
        _disposed = true;
    }
}
