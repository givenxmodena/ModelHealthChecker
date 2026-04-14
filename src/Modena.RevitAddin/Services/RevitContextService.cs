using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;
using Modena.Shared.Enums;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Reads the active Revit document and constructs a ModelIdentity payload.
/// </summary>
public class RevitContextService
{
    private readonly IRevitDocumentContext _context;

    public RevitContextService(IRevitDocumentContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Builds a ModelIdentity from the current Revit document context.
    /// Returns null if no document is open or the document is not saved.
    /// </summary>
    public ModelIdentity? BuildModelIdentity()
    {
        if (!_context.HasActiveDocument)
        {
            LogService.Info("No active document found.");
            return null;
        }

        if (!_context.IsDocumentSaved)
        {
            LogService.Info("Active document has not been saved.");
            return null;
        }

        var modelSource = DetermineModelSource();
        var modelPath = ResolveModelPath();

        var identity = new ModelIdentity
        {
            DocumentTitle = _context.DocumentTitle ?? string.Empty,
            ModelPath = modelPath,
            RevitVersion = _context.RevitVersion ?? string.Empty,
            IsCloudModel = _context.IsCloudModel,
            ProjectGuid = _context.IsCloudModel ? _context.ProjectGuid : null,
            ModelGuid = _context.IsCloudModel ? _context.ModelGuid : null,
            ItemId = _context.IsCloudModel ? _context.ItemId : null,
            VersionUrn = _context.IsCloudModel ? _context.VersionUrn : null,
            ModelSource = modelSource
        };

        LogService.Info($"Built ModelIdentity: source={modelSource}, cloud={identity.IsCloudModel}, title={identity.DocumentTitle}");
        return identity;
    }

    private ModelSource DetermineModelSource()
    {
        if (_context.IsCloudModel)
            return ModelSource.ACCCloud;

        if (!string.IsNullOrEmpty(_context.CentralModelPath)
            && _context.CentralModelPath != _context.ModelPath)
            return ModelSource.RevitServer;

        return ModelSource.Local;
    }

    private string ResolveModelPath()
    {
        // Prefer central path for workshared models, fall back to local path
        if (!string.IsNullOrEmpty(_context.CentralModelPath))
            return _context.CentralModelPath;

        return _context.ModelPath ?? string.Empty;
    }
}
