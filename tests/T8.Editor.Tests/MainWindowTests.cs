using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using T8.Core.Patterns;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class MainWindowTests
{
    private static (MainWindow Window, MainWindowViewModel Vm) Show()
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService());
        vm.OpenFolder(TestPaths.Backup);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    [AvaloniaFact]
    public void Window_shows_the_loaded_library()
    {
        var (window, vm) = Show();

        Assert.Equal("T-8 Editor - BACKUP", window.Title);
        Assert.Contains("Loaded 64 patterns", window.FindControl<TextBlock>("StatusText")!.Text);
        Assert.Equal(8, window.GetVisualDescendants().OfType<RhythmEditorView>().Single()
            .FindControl<ItemsControl>("TrackRows")!.ItemCount);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Rhythm_grid_has_a_toggle_per_step_bound_to_the_pattern()
    {
        var (window, vm) = Show();

        var cells = window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("cell")).ToList();
        Assert.Equal(8 * 32, cells.Count);

        var bdStep1 = cells[(int)RhythmTrack.BD * 32];
        Assert.Equal(vm.CurrentRhythm.Steps[0][RhythmTrack.BD].On, bdStep1.IsChecked);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Clicking_a_cell_toggles_it_and_selects_it()
    {
        var (window, vm) = Show();
        var cells = window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("cell")).ToList();
        var sdStep2 = cells[(int)RhythmTrack.SD * 32 + 1];
        var before = vm.CurrentRhythm.Steps[1][RhythmTrack.SD].On;

        Click(window, sdStep2);

        Assert.Equal(!before, vm.CurrentRhythm.Steps[1][RhythmTrack.SD].On);
        Assert.Equal(RhythmTrack.SD, vm.Rhythm.SelectedCell?.Track);
        Assert.Equal(2, vm.Rhythm.SelectedCell?.StepNumber);
        Assert.True(vm.IsDirty);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Picking_a_pattern_in_the_selector_switches_pattern()
    {
        var (window, vm) = Show();
        var picker = window.FindControl<ListBox>("PatternPicker")!;

        picker.SelectedItem = 5;

        Assert.Equal(5, vm.SelectedPattern);
        Assert.Equal(vm.Library.Rhythm[0, 4].Length, vm.Rhythm.Length);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Bass_tab_shows_32_steps()
    {
        var (window, vm) = Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
        window.UpdateLayout();

        var bass = window.GetVisualDescendants().OfType<BassEditorView>().Single();
        Assert.Equal(32, bass.FindControl<ItemsControl>("BassSteps")!.ItemCount);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Renders_a_screenshot()
    {
        var (window, vm) = Show();
        vm.Rhythm.Tracks[2].Cells[1].ActivateCommand.Execute(null);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var output = Environment.GetEnvironmentVariable("T8_SCREENSHOT_DIR");
        if (output is not null)
        {
            frame!.Save(Path.Combine(output, "rhythm.png"));
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "bass.png"));
        }
        window.Close();
        vm.Dispose();
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
