using Autodesk.Revit.DB;
using Modena.RevitAddin.Services;

namespace Modena.RevitAddin;

/// <summary>
/// Concrete adapter that implements IRevitDocumentContext by wrapping a real
/// Autodesk.Revit.DB.Document. Keeps the interface intact so tests can mock it.
/// </summary>
public class RevitDocumentContext : RevitApi.IRevitDocumentContext
{
    private readonly Document _doc;
    private readonly string _revitVersion;

    public RevitDocumentContext(Document document, string revitVersion)
    {
        _doc = document ?? throw new ArgumentNullException(nameof(document));
        _revitVersion = revitVersion ?? string.Empty;
    }

    public bool HasActiveDocument => true;

    public string? DocumentTitle => _doc.Title;

    public string? ModelPath => _doc.PathName;

    public string? CentralModelPath
    {
        get
        {
            try
            {
                var centralPath = _doc.GetWorksharingCentralModelPath();
                if (centralPath is null) return null;
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
            }
            catch
            {
                return null;
            }
        }
    }

    public string? RevitVersion => _revitVersion;

    public bool IsCloudModel
    {
        get
        {
            try
            {
                return _doc.IsModelInCloud;
            }
            catch
            {
                return false;
            }
        }
    }

    public string? ProjectGuid
    {
        get
        {
            try
            {
                if (!IsCloudModel) return null;
                var cloudPath = _doc.GetCloudModelPath();
                return cloudPath?.GetProjectGUID().ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    public string? ModelGuid
    {
        get
        {
            try
            {
                if (!IsCloudModel) return null;
                var cloudPath = _doc.GetCloudModelPath();
                return cloudPath?.GetModelGUID().ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    public bool IsDocumentSaved => !string.IsNullOrEmpty(_doc.PathName) || IsCloudModel;

    public string? ItemId
    {
        get
        {
            // ACC item ID is not directly available from the Revit API Document object.
            // This would need to be resolved via APS Data Management API on the backend.
            return null;
        }
    }

    public string? VersionUrn
    {
        get
        {
            // Version URN is not directly available from the Revit API Document object.
            // This would need to be resolved via APS Data Management API on the backend.
            return null;
        }
    }
}
