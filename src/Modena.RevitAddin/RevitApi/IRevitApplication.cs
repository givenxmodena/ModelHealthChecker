namespace Modena.RevitAddin.RevitApi;

/// <summary>
/// Abstraction over Autodesk.Revit.UI.UIControlledApplication for testability.
/// In production, the App.cs adapter calls through to the real Revit API.
/// </summary>
public interface IRevitApplication
{
    void CreateRibbonTab(string tabName);
    IRibbonPanel CreateRibbonPanel(string tabName, string panelName);
}

public interface IRibbonPanel
{
    void AddPushButton(string name, string text, string assemblyPath, string className);
}
