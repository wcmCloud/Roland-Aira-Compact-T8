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

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

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
