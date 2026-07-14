using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Modena.RevitAddin.Services;

namespace Modena.RevitAddin.Views;

public partial class AuthWindow : System.Windows.Window
{
    private readonly string _authUrl;

    /// <summary>Fired when the OAuth code is captured from the redirect URI.</summary>
    public event Action<string>? CodeCaptured;

    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Modena", "ModelHealthChecker", "WebView2");

    public AuthWindow(string authUrl)
    {
        _authUrl = authUrl;
        InitializeComponent();

        Loaded          += OnLoaded;
        CancelButton.Click          += (_, _) => { DialogResult = false; Close(); };
        DownloadWebView2Button.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true });
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: UserDataFolder).ConfigureAwait(true); // stay on UI thread

            await WebView.EnsureCoreWebView2Async(env).ConfigureAwait(true);

            WebView.NavigationStarting  += OnNavigationStarting;
            WebView.NavigationCompleted += OnNavigationCompleted;

            WebView.Source = new Uri(_authUrl);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            LoadingPanel.Visibility  = System.Windows.Visibility.Collapsed;
            FallbackPanel.Visibility = System.Windows.Visibility.Visible;
            LogService.Warn("AuthWindow: WebView2 runtime not found.");
        }
        catch (Exception ex)
        {
            LogService.Error("AuthWindow: Failed to initialise WebView2.", ex);
            System.Windows.MessageBox.Show(
                $"Sign-in window could not be opened:\n{ex.Message}",
                "Sign In Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // The APS redirect lands on http://localhost:8080/?code=...
        if (!e.Uri.StartsWith("http://localhost:8080/", StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;

        var uri     = new Uri(e.Uri);
        var qParams = ParseQueryString(uri.Query);
        var code    = qParams.TryGetValue("code", out var c) ? c : null;

        if (!string.IsNullOrWhiteSpace(code))
        {
            CodeCaptured?.Invoke(code!);
            DialogResult = true;
        }
        else
        {
            var error = (qParams.TryGetValue("error_description", out var ed) ? ed : null)
                     ?? (qParams.TryGetValue("error", out var er) ? er : null)
                     ?? "unknown";
            LogService.Warn($"AuthWindow: Auth redirect with no code. error={error}");
            System.Windows.MessageBox.Show(
                $"Sign-in was not completed.\n{error}",
                "Sign In",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            DialogResult = false;
        }

        Close();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
        WebView.Visibility      = System.Windows.Visibility.Visible;
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var q      = query.TrimStart('?');
        if (string.IsNullOrEmpty(q)) return result;

        foreach (var pair in q.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(pair.Substring(0, idx));
            var val = Uri.UnescapeDataString(pair.Substring(idx + 1));
            result[key] = val;
        }
        return result;
    }
}
