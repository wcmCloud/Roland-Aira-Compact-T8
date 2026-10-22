using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class BassLayoutTests
{
    private static (MainWindow Window, MainWindowViewModel Vm) Show(double width)
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => []);
        vm.OpenFolder(TestPaths.Backup);
        var window = new MainWindow { DataContext = vm, Width = width, Height = 890 };
        window.Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = MainWindowViewModel.BassTab;
        vm.Bass.ShowColours = true; // shows the TONES box too, the widest the row gets
        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        window.UpdateLayout();
        return (window, vm);
    }

    private static Border StepEditor(Window window) => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StepEditorPanel");

    [AvaloniaFact]
    public void In_a_macbook_air_window_the_bass_tab_has_no_sideways_scroll_and_the_row_fits_above_the_keyboard()
    {
        var (window, vm) = Show(1470);
        var editor = window.GetVisualDescendants().OfType<BassEditorView>().Single();
        var scroller = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        var keyboard = window.GetVisualDescendants().OfType<PianoKeyboard>().Single();
        var panel = StepEditor(window);

        Assert.True(window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ChordBox").IsEffectivelyVisible);
        Assert.True(scroller.Extent.Width <= scroller.Viewport.Width + 0.5,
            $"the Bass tab scrolls sideways: content {scroller.Extent.Width:0} px, window {scroller.Viewport.Width:0} px");
        // Keep room to spare: the system font on a real Mac is a little wider than in the tests.
        Assert.True(panel.Bounds.Width <= keyboard.Bounds.Width - 40, $"step editor {panel.Bounds.Width:0} px, keyboard {keyboard.Bounds.Width:0} px");
        Assert.All(new[] { "AdvanceCheck", "ColourCheck" }, name =>
            Assert.Contains("compact", window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(t => t.Name == name).Classes));
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void In_the_narrowest_window_the_row_adds_no_overflow_of_its_own()
    {
        var (window, vm) = Show(900);
        var keyboard = window.GetVisualDescendants().OfType<PianoKeyboard>().Single();
        // At the minimum width the 7-octave keyboard itself scrolls; the step editor row must not stick out beyond it.
        Assert.True(StepEditor(window).Bounds.Right <= keyboard.Bounds.Right + 0.5);
        window.Close();
        vm.Dispose();
    }
}
