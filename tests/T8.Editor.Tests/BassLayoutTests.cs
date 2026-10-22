using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class BassLayoutTests
{
    [AvaloniaFact]
    public void The_step_editor_row_is_no_wider_than_the_keyboard_even_with_colours_on()
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => []);
        vm.OpenFolder(TestPaths.Backup);
        var window = new MainWindow { DataContext = vm, Width = 1470, Height = 890 }; // a MacBook Air window
        window.Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = MainWindowViewModel.BassTab;
        vm.Bass.ShowColours = true; // shows the TONES box too
        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        window.UpdateLayout();

        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StepEditorPanel");
        var keyboard = window.GetVisualDescendants().OfType<PianoKeyboard>().Single();
        Assert.True(window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ChordBox").IsEffectivelyVisible);
        // Keep some room to spare: the system font on a real Mac is a little wider than in the tests.
        Assert.True(panel.Bounds.Width <= keyboard.Bounds.Width - 40, $"step editor {panel.Bounds.Width:0} px, keyboard {keyboard.Bounds.Width:0} px");
        Assert.Contains("compact", window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(t => t.Name == "AdvanceCheck").Classes);
        window.Close();
        vm.Dispose();
    }
}
