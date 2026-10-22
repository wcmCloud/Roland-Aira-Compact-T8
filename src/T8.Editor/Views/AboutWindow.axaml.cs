using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace T8.Editor.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.FullVersion}";
    }

    /// <summary>Runs "Check for updates" and returns the message to show; the button is hidden without it.</summary>
    public Func<Task<string>>? CheckForUpdates
    {
        get;
        set
        {
            field = value;
            UpdatePanel.IsVisible = value is not null;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e) => await RunUpdateCheckAsync();

    /// <summary>The "Check for updates" button.</summary>
    public async Task RunUpdateCheckAsync()
    {
        if (CheckForUpdates is null)
            return;
        CheckUpdatesButton.IsEnabled = false;
        UpdateResultText.Text = "Checking…";
        UpdateResultText.IsVisible = true;
        try
        {
            UpdateResultText.Text = await CheckForUpdates();
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void OnWebsite(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.Website) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser to open; the address is on the button.
        }
    }
}
