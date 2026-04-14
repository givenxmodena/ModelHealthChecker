using Modena.RevitAddin.RevitApi;

namespace Modena.RevitAddin;

/// <summary>
/// Creates the "Modena" ribbon tab and "Model Health Checker" push button in Revit.
/// </summary>
public static class RibbonSetup
{
    public const string TabName = "Modena";
    public const string PanelName = "Model Health";
    public const string ButtonName = "ModelHealthChecker";
    public const string ButtonText = "Model Health Checker";

    public static void CreateRibbon(IRevitApplication application, string assemblyPath)
    {
        application.CreateRibbonTab(TabName);
        var panel = application.CreateRibbonPanel(TabName, PanelName);
        panel.AddPushButton(
            ButtonName,
            ButtonText,
            assemblyPath,
            typeof(ModelHealthCheckerCommand).FullName!);
    }
}
