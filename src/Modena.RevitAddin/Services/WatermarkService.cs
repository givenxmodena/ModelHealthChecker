// TRIAL VERSION CODE - excluded from production builds unless TRIAL_VERSION is defined.
#if TRIAL_VERSION
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Handles trial version UI elements.
/// </summary>
public static class WatermarkService
{
    /// <summary>
    /// Creates a trial banner for the main UI.
    /// </summary>
    public static Border? CreateTrialBanner()
    {
        try
        {
            var trialStatus = TrialManager.GetTrialStatus();

            if (!trialStatus.IsTrialActive && !trialStatus.IsExpired)
                return null;

            var backgroundColor = trialStatus.IsExpired ? Colors.Red :
                                  trialStatus.DaysRemaining <= 3 ? Colors.OrangeRed :
                                  Colors.Orange;

            var timeText = trialStatus.IsMinutesMode ? $"{trialStatus.MinutesRemaining} minutes" : $"{trialStatus.DaysRemaining} days";
            var message = trialStatus.IsExpired
                ? "TRIAL EXPIRED - Purchase license to continue"
                : $"TRIAL VERSION - {timeText}, {trialStatus.RefreshesRemaining} refreshes remaining";

            var banner = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(3),
                ToolTip = "1 refresh = 1 model health check refresh.",
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.Bold,
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            if (trialStatus.IsExpired)
            {
                var purchaseButton = new Button
                {
                    Content = "Purchase License",
                    Margin = new Thickness(10, 0, 0, 0),
                    Padding = new Thickness(10, 2, 10, 2),
                    Background = Brushes.White,
                    Foreground = new SolidColorBrush(backgroundColor),
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                purchaseButton.Click += (_, _) => OpenPurchaseUrl();
                ((StackPanel)banner.Child).Children.Add(purchaseButton);
            }

            return banner;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to create trial banner.", ex);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing trial banner's text and colour to reflect the current trial status.
    /// </summary>
    public static void RefreshBanner(Border? banner)
    {
        if (banner is null) return;

        try
        {
            var trialStatus = TrialManager.GetTrialStatus();

            var backgroundColor = trialStatus.IsExpired ? Colors.Red :
                                  trialStatus.DaysRemaining <= 3 ? Colors.OrangeRed :
                                  Colors.Orange;

            var timeText = trialStatus.IsMinutesMode ? $"{trialStatus.MinutesRemaining} minutes" : $"{trialStatus.DaysRemaining} days";
            var message = trialStatus.IsExpired
                ? "TRIAL EXPIRED - Purchase license to continue"
                : $"TRIAL VERSION - {timeText}, {trialStatus.RefreshesRemaining} refreshes remaining";

            banner.Background = new SolidColorBrush(backgroundColor);

            if (banner.Child is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock textBlock)
                textBlock.Text = message;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to refresh trial banner.", ex);
        }
    }

    /// <summary>
    /// Shows trial status dialog.
    /// </summary>
    public static void ShowTrialStatusDialog(Window? parent = null)
    {
        try
        {
            var trialStatus = TrialManager.GetTrialStatus();

            var dialog = new Window
            {
                Title = "Model Health Checker Trial Status",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = parent,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var content = new StackPanel
            {
                Margin = new Thickness(20, 20, 20, 20),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Model Health Checker Trial Information",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 15)
                    },
                    new TextBlock
                    {
                        Text = $"Trial End Date: {trialStatus.TrialEndDate:yyyy-MM-dd}",
                        Margin = new Thickness(0, 0, 0, 5)
                    },
                    new TextBlock
                    {
                        Text = $"Days Remaining: {trialStatus.DaysRemaining}",
                        Margin = new Thickness(0, 0, 0, 5)
                    },
                    new TextBlock
                    {
                        Text = $"Refreshes Used: {trialStatus.TotalRefreshes} / {trialStatus.TotalRefreshes + trialStatus.RefreshesRemaining}",
                        Margin = new Thickness(0, 0, 0, 15)
                    },
                    new TextBlock
                    {
                        Text = trialStatus.GetStatusMessage(),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15)
                    }
                }
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children =
                {
                    new Button
                    {
                        Content = "Purchase License",
                        Margin = new Thickness(0, 0, 10, 0),
                        Padding = new Thickness(15, 5, 15, 5),
                        IsDefault = trialStatus.IsExpired
                    },
                    new Button
                    {
                        Content = "Close",
                        Padding = new Thickness(15, 5, 15, 5),
                        IsCancel = true
                    }
                }
            };

            ((Button)buttonPanel.Children[0]).Click += (_, _) => { OpenPurchaseUrl(); dialog.Close(); };
            ((Button)buttonPanel.Children[1]).Click += (_, _) => dialog.Close();

            content.Children.Add(buttonPanel);
            dialog.Content = content;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to show trial status dialog.", ex);
        }
    }

    private static void OpenPurchaseUrl()
    {
        try
        {
            var url = "mailto:given@modena-aec.co.za?subject=Model%20Health%20Checker%20License%20Request";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            LogService.Info("Opened purchase URL.");
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to open purchase URL.", ex);
        }
    }
}
#endif // TRIAL_VERSION
