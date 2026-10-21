using Avalonia.Controls.Presenters;
using Avalonia.Media;
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
    private static (MainWindow Window, MainWindowViewModel Vm) Show(params string[] driveCandidates)
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => driveCandidates);
        vm.OpenFolder(TestPaths.Backup);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    [AvaloniaFact]
    public void Window_shows_the_loaded_library()
    {
        var (window, vm) = Show();

        Assert.Equal("Roland Aira Compact T-8 Editor - v1.0.3 - BACKUP", window.Title);
        Assert.Contains("Loaded 64 patterns", window.FindControl<TextBlock>("StatusText")!.Text);
        // CY has no sound on the T-8, so its row is hidden.
        Assert.Equal(7, window.GetVisualDescendants().OfType<RhythmEditorView>().Single()
            .FindControl<ItemsControl>("TrackRows")!.ItemCount);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Rhythm_grid_has_a_toggle_per_step_bound_to_the_pattern()
    {
        var (window, vm) = Show();

        var cells = window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("cell")).ToList();
        Assert.Equal(7 * 32, cells.Count); // CY is hidden

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
    public void Clicking_a_cell_in_the_pattern_grid_switches_bank_and_pattern()
    {
        var (window, vm) = Show();
        window.UpdateLayout();
        var grid = window.FindControl<ItemsControl>("PatternGrid")!;
        var cell = grid.GetVisualDescendants().OfType<Button>()
            .Single(b => b.DataContext is PatternSlotViewModel { Bank: 3, Pattern: 5 });

        Click(window, cell);

        Assert.Equal((3, 5), (vm.SelectedBank, vm.SelectedPattern));
        Assert.Equal(vm.Library.Rhythm[2, 4].Length, vm.Rhythm.Length);
        Assert.Contains("selected", cell.Classes);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Pattern_knobs_set_last_step_shuffle_and_flam()
    {
        var (window, vm) = Show();
        window.UpdateLayout();
        var rhythm = window.GetVisualDescendants().OfType<RhythmEditorView>().Single();

        rhythm.FindControl<Knob>("RhythmLength")!.Value = 12;
        rhythm.FindControl<Knob>("ShuffleInput")!.Value = 60;
        rhythm.FindControl<Knob>("FlamInput")!.Nudge(2);

        Assert.Equal(12, vm.CurrentRhythm.Length);
        Assert.Equal(60, vm.CurrentRhythm.Shuffle);
        Assert.Equal(vm.Rhythm.Flam, (int)rhythm.FindControl<Knob>("FlamInput")!.Value);
        Assert.True(vm.IsDirty);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Ctrl_click_picks_active_cells_without_switching_them()
    {
        var (window, vm) = Show();
        window.UpdateLayout();
        var bd = vm.Rhythm[RhythmTrack.BD].Cells.First(c => c.On);
        var off = vm.Rhythm[RhythmTrack.SD].Cells.First(c => !c.On);
        var buttons = window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("cell")).ToList();

        foreach (var cell in new[] { bd, off })
        {
            var button = buttons.Single(b => b.DataContext == cell);
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
        }

        Assert.True(bd.On);          // not switched off
        Assert.True(bd.IsMultiSelected);
        Assert.False(off.On);        // not switched on
        Assert.False(off.IsMultiSelected);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "MultiSelectionText" && t.IsEffectivelyVisible);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Each_row_has_copy_paste_and_clear_and_paste_turns_on_after_a_copy()
    {
        var (window, vm) = Show();
        window.UpdateLayout();
        var tools = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("rowTool")).ToList();
        Assert.Equal(7 * 3, tools.Count);
        var paste = tools.Where((_, i) => i % 3 == 1).ToList();
        Assert.All(paste, p => Assert.False(p.IsEnabled));

        Click(window, tools[0]); // copy the first row
        window.UpdateLayout();

        Assert.All(paste, p => Assert.True(p.IsEnabled));
        Assert.True(vm.Rhythm.Tracks[0].IsSelected);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void The_rhythm_bass_and_song_tabs_have_icons_and_switch()
    {
        var (window, vm) = Show();
        var tabs = window.FindControl<TabControl>("Tabs")!;
        foreach (var name in new[] { "RhythmTab", "BassTab", "SongTab" })
        {
            var tab = window.FindControl<TabItem>(name)!;
            var header = Assert.IsType<StackPanel>(tab.Header);
            Assert.NotNull(header.Children.OfType<PathIcon>().Single().Data);
        }
        // The open tab is an orange pill with white text; the others are see-through with grey text.
        window.UpdateLayout();
        static Border Pill(TabItem tab) => tab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_LayoutRoot");
        static IBrush? Text(TabItem tab) => tab.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Foreground;
        var rhythm = window.FindControl<TabItem>("RhythmTab")!;
        var bass = window.FindControl<TabItem>("BassTab")!;
        Assert.IsType<LinearGradientBrush>(Pill(rhythm).Background);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)Pill(bass).Background!).Color);
        Assert.Equal(Colors.White, ((ISolidColorBrush)Text(rhythm)!).Color);
        Assert.NotEqual(Colors.White, ((ISolidColorBrush)Text(bass)!).Color);
        Assert.NotNull(ToolTip.GetTip(bass));

        tabs.SelectedItem = window.FindControl<TabItem>("SongTab");
        Assert.Equal(2, vm.SelectedTab);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Rhythm_generator_has_instrument_chips_with_icons_and_outlined_combos()
    {
        var (window, vm) = Show();
        var generator = window.GetVisualDescendants().OfType<RhythmGeneratorView>().Single();

        var chips = new[] { "BassDrumChip", "SnareChip", "TomChip", "ClapChip", "ClosedHatChip", "OpenHatChip", "FillCheck" }
            .Select(name => generator.FindControl<ToggleButton>(name)!).ToList();
        Assert.All(chips, chip => Assert.NotNull(chip.GetVisualDescendants().OfType<PathIcon>().Single().Data));
        Assert.All(chips, chip => Assert.NotNull(ToolTip.GetTip(chip)));

        var snare = generator.FindControl<ToggleButton>("SnareChip")!;
        var wasOn = vm.RhythmGenerator.Snare;
        snare.IsChecked = !wasOn;
        Assert.Equal(!wasOn, vm.RhythmGenerator.Snare);

        Assert.Contains("port", generator.FindControl<ComboBox>("StylePicker")!.Classes);
        Assert.Contains("port", generator.FindControl<ComboBox>("FillPatternPicker")!.Classes);
        Assert.Contains("port", generator.FindControl<NumericUpDown>("RhythmSeedInput")!.Classes);
        Assert.IsType<StackPanel>(generator.FindControl<Button>("GenerateRhythmButton")!.Content);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Bass_generator_uses_the_outlined_combos_and_icons()
    {
        var (window, vm) = Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
        window.UpdateLayout();
        var generator = window.GetVisualDescendants().OfType<BassGeneratorView>().Single();

        foreach (var name in new[] { "KeyPicker", "ScalePicker", "ProgressionPicker", "ChordLengthPicker" })
            Assert.Contains("port", generator.FindControl<ComboBox>(name)!.Classes);
        Assert.Equal(4, generator.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("fieldBox")));
        Assert.Contains("port", generator.FindControl<NumericUpDown>("SeedInput")!.Classes);
        Assert.IsType<StackPanel>(generator.FindControl<Button>("GenerateButton")!.Content);
        Assert.IsType<StackPanel>(generator.FindControl<Button>("UndoGenerateButton")!.Content);

        // Last step, Triplet and Transpose sit in the PATTERN panel above the steps, in outlined boxes.
        var editor = window.GetVisualDescendants().OfType<BassEditorView>().Single();
        var panel = editor.FindControl<Border>("BassPatternPanel")!;
        var length = editor.FindControl<NumericUpDown>("BassLength")!;
        var triplet = editor.FindControl<ToggleButton>("TripletCheck")!;
        var up12 = editor.FindControl<Button>("TransposeUp12Button")!;
        Assert.All(new Control[] { length, triplet, up12 }, c => Assert.Contains(panel, c.GetVisualAncestors()));
        Assert.Contains("port", length.Classes);
        Assert.Contains("fieldBox", length.FindAncestorOfType<Border>()!.Classes);
        Assert.Contains("fieldBox", editor.FindControl<Border>("TransposeBox")!.Classes);
        var labels = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("PATTERN", labels);
        Assert.Contains("LAST STEP", labels);
        Assert.Contains("TRANSPOSE", labels);
        Assert.Contains("TRIPLET", labels);

        Assert.Contains("chip", triplet.Classes);
        triplet.IsChecked = !vm.Bass.Triplet;
        Assert.Equal(triplet.IsChecked, vm.Bass.Triplet);

        // The STEP EDITOR panel: chevron chips around an outlined readout, the auto-advance chip and the hint.
        var stepPanel = editor.FindControl<Border>("StepEditorPanel")!;
        var previous = editor.FindControl<Button>("PreviousStepButton")!;
        var next = editor.FindControl<Button>("NextStepButton")!;
        var advance = editor.FindControl<ToggleButton>("AdvanceCheck")!;
        var readout = editor.FindControl<Border>("StepPickerBox")!;
        var hint = editor.FindControl<StackPanel>("StepEditorHint")!;
        Assert.All(new Control[] { previous, next, advance, readout, hint }, c => Assert.Contains(stepPanel, c.GetVisualAncestors()));
        Assert.Contains("STEP EDITOR", stepPanel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Contains("fieldBox", readout.Classes);
        Assert.All(new Control[] { previous, next, advance, hint },
            c => Assert.NotNull(c.GetVisualDescendants().OfType<PathIcon>().First().Data));
        Assert.Contains("chip", advance.Classes);
        advance.IsChecked = !vm.Bass.AdvanceAfterNote;
        Assert.Equal(advance.IsChecked, vm.Bass.AdvanceAfterNote);
        vm.Bass.SelectedStep = vm.Bass.Steps[1];
        next.Command!.Execute(null);
        Assert.Same(vm.Bass.Steps[2], vm.Bass.SelectedStep);
        previous.Command!.Execute(null);
        previous.Command!.Execute(null);
        Assert.Same(vm.Bass.Steps[0], vm.Bass.SelectedStep);

        var before = vm.Bass.Steps[0].NoteName;
        up12.Command!.Execute(up12.CommandParameter);
        Assert.NotEqual(before, vm.Bass.Steps[0].NoteName);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Song_tab_buttons_have_icons_combos_are_outlined_and_rows_still_work()
    {
        var (window, vm) = Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 2;
        vm.Song.AddCurrentPatternCommand.Execute(null);
        vm.Song.AddCurrentPatternCommand.Execute(null);
        window.UpdateLayout();
        var song = window.GetVisualDescendants().OfType<SongView>().Single();

        foreach (var name in new[] { "CopyToBankButton", "NewSongButton", "OpenSongButton", "SaveSongButton", "SaveSongAsButton",
                     "AddCurrentPatternButton", "PlaySongButton", "StopSongButton", "PreviousEntryButton", "NextEntryButton" })
            Assert.NotNull(song.FindControl<Button>(name)!.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        foreach (var name in new[] { "CopyBankPicker", "LoopEntryPicker" })
            Assert.Contains("port", song.FindControl<ComboBox>(name)!.Classes);
        Assert.Contains("field", song.FindControl<TextBox>("SongTitleBox")!.Classes);
        var labels = song.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("TITLE", labels);
        Assert.Contains("COPY TO BANK", labels);
        Assert.Contains("LOOP", labels);

        // Each row: outlined combos and repeat field, a FILL chip with an icon, and five icon buttons (delete in red).
        var rows = song.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("songRow")).ToList();
        Assert.Equal(2, rows.Count);
        var row = rows[0];
        Assert.Equal(3, row.GetVisualDescendants().OfType<ComboBox>().Count(c => c.Classes.Contains("port")));
        Assert.Contains("port", row.GetVisualDescendants().OfType<NumericUpDown>().Single().Classes);
        var tools = row.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("rowTool")).ToList();
        Assert.Equal(5, tools.Count);
        Assert.All(tools, b => Assert.NotNull(b.GetVisualDescendants().OfType<PathIcon>().Single().Data));
        Assert.Contains("danger", tools[4].Classes);

        var fill = row.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Classes.Contains("chip"));
        Assert.NotNull(fill.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        var hadFill = vm.Song.Entries[0].HasFill;
        fill.IsChecked = !hadFill;
        Assert.Equal(!hadFill, vm.Song.Entries[0].HasFill);

        tools[4].Command!.Execute(tools[4].CommandParameter); // delete
        Assert.Single(vm.Song.Entries);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Each_row_name_has_its_own_drum_icon()
    {
        var (window, vm) = Show();
        window.UpdateLayout();
        var icons = window.GetVisualDescendants().OfType<PathIcon>().Where(p => p.Classes.Contains("trackIcon")).ToList();

        Assert.Equal(7, icons.Count);
        Assert.All(icons, icon => Assert.NotNull(icon.Data));
        Assert.Equal(7, icons.Select(icon => icon.Data).Distinct().Count());

        // Muted until the row is selected, then orange; the name buttons keep their size and tooltip.
        var names = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("trackName")).ToList();
        Assert.All(names, b => Assert.Equal(118, b.Width));
        Assert.All(names, b => Assert.NotNull(ToolTip.GetTip(b)));
        Assert.Equal(Avalonia.Media.Color.Parse("#B4B4BD"), ((Avalonia.Media.ISolidColorBrush)icons[0].Foreground!).Color);
        vm.Rhythm.SelectTrackCommand.Execute(vm.Rhythm.Tracks[0]);
        window.UpdateLayout();
        Assert.Equal(Avalonia.Media.Color.Parse("#FF7A1A"), ((Avalonia.Media.ISolidColorBrush)icons[0].Foreground!).Color);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Knobs_turn_with_a_drag_and_the_wheel_and_reset()
    {
        var knob = new Knob { Minimum = 0, Maximum = 100, Value = 50, DefaultValue = 20 };
        var window = new Window { Content = knob, Width = 200, Height = 200 };
        window.Show();
        window.UpdateLayout();
        var center = knob.TranslatePoint(new Point(knob.Bounds.Width / 2, 20), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center - new Point(0, 40)); // up 40 px of 160 for the range
        window.MouseUp(center - new Point(0, 40), MouseButton.Left);
        Assert.Equal(75, knob.Value);

        knob.Nudge(-3);
        Assert.Equal(72, knob.Value);
        knob.Value = 500;
        Assert.Equal(100, knob.Value);

        knob.Reset();
        Assert.Equal(20, knob.Value);
        window.Close();
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

            vm.BassGenerator.GenerateCommand.Execute(null);
            vm.Bass.ShowPlayingStep(4);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "bass-playing.png"));
            vm.RhythmGenerator.GenerateCommand.Execute(null);
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

public class AboutWindowTests
{
    [AvaloniaFact]
    public void About_shows_the_author_and_version_and_no_avalonia_branding()
    {
        var about = new AboutWindow();
        about.Show();
        var texts = about.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

        Assert.Contains("Christos Christodoulidis", texts);
        Assert.Contains(texts, t => t.StartsWith("Version 1.0.3"));
        Assert.DoesNotContain(texts, t => t.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        about.Close();
    }

    [AvaloniaFact]
    public void The_windows_and_about_show_the_app_icon()
    {
        var about = new AboutWindow();
        about.Show();
        Assert.NotNull(about.Icon);
        var image = about.FindControl<Image>("AppIconImage")!;
        Assert.Equal(256, ((Avalonia.Media.Imaging.Bitmap)image.Source!).PixelSize.Width);
        about.Close();

        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => []);
        var main = new MainWindow { DataContext = vm };
        Assert.NotNull(main.Icon);
        vm.Dispose();
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

        // OFF is the first choice for bass and rhythm, shown as "OFF".
        var rhythmPicker = window.FindControl<ComboBox>("RhythmChannelPicker")!;
        Assert.Equal(0, rhythmPicker.Items[0]);
        Assert.Equal(17, rhythmPicker.ItemCount);
        rhythmPicker.SelectedItem = 0;
        window.UpdateLayout();
        Assert.Contains(rhythmPicker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "OFF");
        Assert.True(window.FindControl<Button>("SaveSettingsButton")!.IsEnabled);
        Assert.Equal(0, vm.ToSettings().Midi.RhythmChannel);
        Assert.Contains("rhCh to OFF", vm.T8SetupHelp);

        var output = Environment.GetEnvironmentVariable("T8_SCREENSHOT_DIR");
        if (output is not null)
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "midi-settings.png"));
        window.Close();
    }

    [AvaloniaFact]
    public void Load_and_write_buttons_follow_the_t8_drive_and_check_finds_it()
    {
        var drives = new List<string>();
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => drives);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        var load = window.FindControl<Button>("LoadFromT8Button")!;
        var write = window.FindControl<Button>("WriteToT8Button")!;
        Assert.False(load.IsEffectivelyEnabled);
        Assert.False(write.IsEffectivelyEnabled);
        Assert.NotNull(window.FindControl<Button>("T8DriveHelpButton")!.Flyout);

        // The test data folder holds a BACKUP folder like the T-8 drive.
        drives.Add(Path.GetDirectoryName(TestPaths.Backup)!);
        window.FindControl<Button>("CheckT8DriveButton")!.Command!.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(load.IsEffectivelyEnabled);
        Assert.True(write.IsEffectivelyEnabled);
        Assert.Contains("on", window.FindControl<Avalonia.Controls.Shapes.Ellipse>("T8DriveDot")!.Classes);
        window.Close();
        vm.Dispose();
    }
}
