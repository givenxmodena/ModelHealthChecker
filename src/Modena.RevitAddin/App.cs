using Autodesk.Revit.UI;
using Modena.RevitAddin.Services;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

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
            // Configure file logging before anything else.
            var config   = PluginConfig.Load();
            var fileName = $"ModelHealthChecker-{DateTime.Now:yyyy-MM-dd}.log";

            // Primary: %APPDATA%\Modena\logs\ — persists across Revit sessions.
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Modena", "logs", fileName);

            // Secondary: logs\ folder next to the add-in DLL — easy to find during development.
            var assemblyDir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var localLogPath = Path.Combine(assemblyDir, "logs", fileName);

            LogService.Configure(config.LoggingLevel, appDataPath, localLogPath);
            LogService.Info("LogService configured. Add-in starting.");

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

            buttonData.LargeImage = LoadIcon("Resources/health-check_32.png");
            buttonData.Image      = LoadIcon("Resources/health-check_16.png");

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

    private static BitmapImage? LoadIcon(string resourcePath)
    {
        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(App).Assembly.Location) ?? string.Empty;
            var fullPath    = Path.Combine(assemblyDir, resourcePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                LogService.Warn($"Icon not found at '{fullPath}'.");
                return null;
            }
            var img = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
            img.Freeze();
            return img;
        }
        catch (Exception ex)
        {
            LogService.Warn($"Could not load icon '{resourcePath}': {ex.Message}");
            return null;
        }
    }
}
