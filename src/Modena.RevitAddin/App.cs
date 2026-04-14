using Autodesk.Revit.UI;
using Modena.RevitAddin.Services;
using System.Reflection;

namespace Modena.RevitAddin;

/// <summary>
/// Revit add-in entry point. Creates the "Modena" ribbon tab and
/// "Model Health Checker" push button when Revit starts.
/// </summary>
public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // Create or reuse the "Modena" ribbon tab
            const string tabName = "Modena";
            try
            {
                application.CreateRibbonTab(tabName);
                LogService.Info($"Created ribbon tab: {tabName}");
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists (from another Modena add-in)
                LogService.Info($"Ribbon tab '{tabName}' already exists, reusing.");
            }

            // Create panel and button
            var panel = application.CreateRibbonPanel(tabName, "Model Health");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            var buttonData = new PushButtonData(
                "ModelHealthChecker",
                "Model Health\nChecker",
                assemblyPath,
                typeof(ModelHealthCheckerCommand).FullName!);

            buttonData.ToolTip = "Open the Model Health Checker dashboard";
            buttonData.LongDescription =
                "Displays model health KPIs, failed checks, families, and element categories for the active Revit model.";

            panel.AddItem(buttonData);

            LogService.Info("Modena Model Health Checker add-in started successfully.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to start Modena add-in.", ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        LogService.Info("Modena Model Health Checker add-in shutting down.");
        return Result.Succeeded;
    }
}
