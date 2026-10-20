using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Midi.Ports;

namespace T8.Editor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(MidiPorts.System, new AvaloniaDialogService(window));
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
