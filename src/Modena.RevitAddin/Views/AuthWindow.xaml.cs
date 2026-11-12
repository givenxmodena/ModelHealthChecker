using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Modena.RevitAddin.Services;

namespace Modena.RevitAddin.Views;

public partial class AuthWindow : System.Windows.Window
{
    private readonly string _authUrl;
    private TaskCompletionSource<string?>? _completion;
    private CancellationTokenRegistration _cancellation;
    private string? _code;
    private bool _closed;

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
        CancelButton.Click          += (_, _) => Finish(false);
        Closed += (_, _) =>
        {
            _closed = true;
            _cancellation.Dispose();
            _completion?.TrySetResult(_code);
            WebView.Dispose();
        };
        DownloadWebView2Button.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true });
    }

    public Task<string?> ShowForCodeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellation = ct.Register(() =>
        {
            _completion.TrySetCanceled();
            Dispatcher.BeginInvoke(new Action(Close));
        });
        Show();
        return _completion.Task;
    }

    private void Finish(bool success)
    {
        if (_closed) return;
        if (_completion is null) DialogResult = success;
        Close();
    }

    public static bool TryGetAuthorizationCode(string authUrl, string callbackUrl, out string? code)
    {
        code = null;
        if (!Uri.TryCreate(authUrl, UriKind.Absolute, out var authorization)
            || !Uri.TryCreate(callbackUrl, UriKind.Absolute, out var callback)) return false;
        var expected = ParseQueryString(authorization.Query);
        if (!expected.TryGetValue("redirect_uri", out var redirect)
            || !Uri.TryCreate(redirect, UriKind.Absolute, out var target)
            || callback.Scheme != target.Scheme || callback.Host != target.Host || callback.Port != target.Port
            || callback.AbsolutePath != target.AbsolutePath || callback.UserInfo.Length != 0 || callback.Fragment.Length != 0) return false;
        var query = ParseQueryString(callback.Query);
        if (!expected.TryGetValue("state", out var state) || string.IsNullOrEmpty(state)
            || !query.TryGetValue("state", out var actual) || actual != state
            || query.ContainsKey("error") || !query.TryGetValue("code", out var result) || string.IsNullOrWhiteSpace(result)) return false;
        code = result;
        return true;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: UserDataFolder).ConfigureAwait(true); // stay on UI thread
            if (_closed) return;

            await WebView.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            if (_closed) return;

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
        catch (Exception)
        {
            if (_closed) return;
            LogService.Warn("AuthWindow: Could not initialise Autodesk sign-in.");
            System.Windows.MessageBox.Show(
                "Sign-in window could not be opened. Check WebView2 installation and try again.",
                "Sign In Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Finish(false);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // The APS redirect lands on http://localhost:8080/?code=...
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var callback)
            || callback.Scheme != "http" || callback.Host != "localhost" || callback.Port != 8080
            || callback.AbsolutePath != "/") return;

        e.Cancel = true;

        if (TryGetAuthorizationCode(_authUrl, e.Uri, out var code))
        {
            _code = code;
            CodeCaptured?.Invoke(code!);
            Finish(true);
        }
        else
        {
            LogService.Warn("AuthWindow: Autodesk sign-in was rejected or its callback was invalid.");
            System.Windows.MessageBox.Show(
                "Sign-in was not completed. Please try again.",
                "Sign In",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            Finish(false);
        }
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
            if (result.ContainsKey(key)) return new Dictionary<string, string>();
            result[key] = val;
        }
        return result;
    }
}
