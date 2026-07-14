using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.RevitAddin.Views;

namespace Modena.RevitAddin;

/// <summary>
/// IExternalCommand that opens the Model Health Checker dashboard.
/// Extracts model identity from the active Revit document and launches the WPF window.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ModelHealthCheckerCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            LogService.Info("ModelHealthCheckerCommand: Execute started.");

#if TRIAL_VERSION
            var trialStatus = TrialManager.GetTrialStatus();
            if (!trialStatus.IsTrialActive)
            {
                LogService.Warn("ModelHealthCheckerCommand: Trial inactive. Command cancelled.");
                WatermarkService.ShowTrialStatusDialog();
                return Result.Cancelled;
            }
#endif

            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument?.Document;

            if (doc is null)
            {
                LogService.Warn("ModelHealthCheckerCommand: No active document.");
                TaskDialog.Show("Pulse",
                    "No active document found. Please open a Revit model first.");
                return Result.Failed;
            }

            var documentContext = new RevitDocumentContext(doc, uiApp.ActiveUIDocument, uiApp.Application.VersionNumber);
            var contextService = new RevitContextService(documentContext);
            var identity = contextService.BuildModelIdentity();

            if (identity is null)
            {
                LogService.Warn("ModelHealthCheckerCommand: Could not build model identity.");
                TaskDialog.Show("Pulse",
                    "No active saved document found. Please open and save a Revit model first.");
                return Result.Failed;
            }

            var config = PluginConfig.Load();

            // Optional authentication gate
            if (config.RequireAuthentication)
            {
                var (clientId, clientSecret) = ApsCredentialsProvider.GetCredentials();
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    TaskDialog.Show("Pulse",
                        "Authentication is required but APS credentials are not configured.\n" +
                        "Contact your administrator.");
                    return Result.Cancelled;
                }

                var auth = new AuthService(clientId!, clientSecret!);
                try
                {
                    var email = auth.AuthenticateUserAsync(config.AllowedDomains)
                                   .GetAwaiter().GetResult();
                    if (email is null)
                    {
                        LogService.Info("ModelHealthCheckerCommand: User cancelled sign-in.");
                        return Result.Cancelled;
                    }
                    LogService.Info($"ModelHealthCheckerCommand: Authenticated as {email}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    TaskDialog.Show("Pulse — Access Denied", ex.Message);
                    return Result.Cancelled;
                }
                catch (Exception ex)
                {
                    LogService.Error("ModelHealthCheckerCommand: Authentication failed.", ex);
                    TaskDialog.Show("Pulse",
                        $"Sign-in failed: {ex.Message}");
                    return Result.Failed;
                }
            }

            var extractor = new RevitHealthExtractor(doc, config);

            var viewModel = new ModelHealthViewModel(identity, extractor, documentContext, config);
            var window = new ModelHealthWindow(viewModel);

            LogService.Info("ModelHealthCheckerCommand: Opening dashboard window.");
            window.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            LogService.Error("ModelHealthCheckerCommand: Unhandled exception.", ex);
            TaskDialog.Show("Pulse",
                $"An unexpected error occurred: {ex.Message}");
            message = ex.Message;
            return Result.Failed;
        }
    }
}
