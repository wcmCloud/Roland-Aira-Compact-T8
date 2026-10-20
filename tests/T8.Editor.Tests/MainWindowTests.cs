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
    public void Clicking_a_piano_key_sets_the_selected_steps_note()
    {
        var (window, vm) = Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
        window.UpdateLayout();
        var bass = window.GetVisualDescendants().OfType<BassEditorView>().Single();
        var keyboard = bass.FindControl<PianoKeyboard>("BassKeyboard")!;
        vm.Bass.SelectedStep = vm.Bass.Steps[2];

        // The first white key is C0 (12); the second is D0 (14). Click low on the key, below the black keys.
        var point = keyboard.TranslatePoint(new Point(PianoKeyboard.WhiteWidth * 1.5, 100), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        Assert.Equal(14, vm.Bass.Steps[2].Note);
        Assert.Equal(T8.Core.Patterns.BassStepState.Note, vm.Bass.Steps[2].State);
        Assert.Same(vm.Bass.Steps[3], vm.Bass.SelectedStep);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Step_menu_lists_every_note_by_octave_and_sets_it()
    {
        var (window, vm) = Show();
        var step = vm.Bass.Steps[0];
        var menu = BassEditorView.BuildStepMenu(step);
        var groups = menu.ItemsSource!.OfType<MenuItem>().Where(m => ((string)m.Header!).StartsWith("Octave")).ToList();

        Assert.Equal(8, groups.Count);
        Assert.Equal(85, groups.Sum(g => ((IEnumerable<MenuItem>)g.ItemsSource!).Count()));
        var g3 = ((IEnumerable<MenuItem>)groups[2].ItemsSource!).Single(m => (string)m.Header! == "G2");
        g3.Command!.Execute(null);
        Assert.Equal(43, step.Note);
        Assert.True(step.HasNote);

        var tie = menu.ItemsSource!.OfType<MenuItem>().Single(m => (string)m.Header! == "Tie");
        tie.Command!.Execute(tie.CommandParameter);
        Assert.Equal(T8.Core.Patterns.BassStepState.Tie, step.State);
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

            vm.Bass.ShowPlayingStep(4);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "bass-playing.png"));
            vm.Rhythm.ShowPlayingStep(4);
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = 0;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "rhythm-playing.png"));
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

public class MidiSettingsWindowTests
{
    [AvaloniaFact]
    public void Settings_window_shows_channels_and_saves()
    {
        var vm = new MidiSettingsViewModel(FakePortProvider.WithT8(), new T8.Editor.Services.EditorSettings());
        var window = new MidiSettingsWindow { DataContext = vm };
        window.Show();

        Assert.Equal(2, window.FindControl<ComboBox>("BassChannelPicker")!.SelectedItem);
        Assert.Equal(10, window.FindControl<ComboBox>("RhythmChannelPicker")!.SelectedItem);
        Assert.Equal("T-8", (window.FindControl<ComboBox>("SettingsPortPicker")!.SelectedItem as T8.Midi.Ports.MidiPortInfo)?.Name);
        Assert.True(window.FindControl<RadioButton>("PreviewModeRadio")!.IsChecked);

        window.FindControl<ComboBox>("BassChannelPicker")!.SelectedItem = 10;
        Assert.False(window.FindControl<Button>("SaveSettingsButton")!.IsEnabled);
        window.FindControl<ComboBox>("BassChannelPicker")!.SelectedItem = 3;
        Assert.True(window.FindControl<Button>("SaveSettingsButton")!.IsEnabled);

        window.FindControl<RadioButton>("SyncModeRadio")!.IsChecked = true;
        Assert.Equal(T8.Editor.Services.PlayMode.SyncT8Sequencer, vm.PlayMode);
        Assert.Equal(3, vm.ToSettings().Midi.BassChannel);

        var output = Environment.GetEnvironmentVariable("T8_SCREENSHOT_DIR");
        if (output is not null)
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "midi-settings.png"));
        window.Close();
    }
}
