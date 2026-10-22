using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class NotesFromT8Tests
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();

    private MainWindowViewModel Create(EditorSettings? settings = null)
    {
        var vm = new MainWindowViewModel(_ports, new FakeDialogService(), new MemorySettingsStore { Stored = settings ?? new EditorSettings() }, () => []);
        vm.OpenFolder(TestPaths.Backup);
        vm.SelectedTab = MainWindowViewModel.BassTab;
        return vm;
    }

    [Fact]
    public void A_key_played_on_the_t8_goes_into_the_selected_step_and_moves_on()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = vm.Bass.Steps[2];
        _ports.LastOpened?.Clear();

        vm.OnT8BassNote(45, pressed: true); // A2

        Assert.Equal(45, vm.Bass.Steps[2].Note);
        Assert.Equal(T8.Core.Patterns.BassStepState.Note, vm.Bass.Steps[2].State);
        Assert.Same(vm.Bass.Steps[3], vm.Bass.SelectedStep); // next step after a note
        Assert.Equal(45, vm.Bass.T8KeyNote); // shown on the keyboard
        Assert.Contains("played on the T-8", vm.Status);
        Assert.True(vm.IsDirty);
        // Not echoed back: the T-8 already sounds the key.
        Assert.DoesNotContain(_ports.Opened.SelectMany(o => o.Events), e => e is NoteOnEvent);

        vm.OnT8BassNote(45, pressed: false);
        Assert.Null(vm.Bass.T8KeyNote);
    }

    [Fact]
    public void Keys_only_light_up_on_other_tabs_or_with_the_option_off()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        var before = vm.Bass.Steps[0].Note;

        vm.SelectedTab = 0;
        vm.OnT8BassNote(50, pressed: true);
        Assert.Equal(before, vm.Bass.Steps[0].Note);
        Assert.Equal(50, vm.Bass.T8KeyNote);

        vm.SelectedTab = MainWindowViewModel.BassTab;
        vm.NotesFromT8 = false;
        vm.OnT8BassNote(52, pressed: true);
        Assert.Equal(before, vm.Bass.Steps[0].Note);
        Assert.Equal(52, vm.Bass.T8KeyNote);
        Assert.False(vm.Settings.NotesFromT8);
    }

    [Fact]
    public void Notes_outside_the_t8_range_or_without_a_selection_are_ignored()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = null;
        var notes = vm.Bass.Steps.Select(s => (s.Note, s.State)).ToList();
        vm.OnT8BassNote(45, pressed: true);
        Assert.Equal(notes, vm.Bass.Steps.Select(s => (s.Note, s.State)));
        Assert.Equal(45, vm.Bass.T8KeyNote);

        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        var before = vm.Bass.Steps[0].Note;
        vm.OnT8BassNote(BassEditorViewModel.HighestNote + 5, pressed: true);
        Assert.Equal(before, vm.Bass.Steps[0].Note);
        Assert.Same(vm.Bass.Steps[0], vm.Bass.SelectedStep);
    }

    [AvaloniaFact]
    public void Keys_from_the_t8s_midi_input_reach_the_bass_editor_and_the_keyboard()
    {
        using var vm = Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = MainWindowViewModel.BassTab;
        vm.Bass.SelectedStep = vm.Bass.Steps[1];
        window.UpdateLayout();

        var input = _ports.OpenedInputs.Last();
        input.Receive(new NoteOnEvent((SevenBitNumber)43, (SevenBitNumber)100) { Channel = (FourBitNumber)(vm.Settings.Midi.BassChannel - 1) });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(43, vm.Bass.Steps[1].Note);
        var keyboard = window.GetVisualDescendants().OfType<PianoKeyboard>().Single();
        Assert.Equal(43, keyboard.T8KeyNote);
        var chip = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(t => t.Name == "NotesFromT8Toggle");
        Assert.True(chip.IsChecked);
        Assert.True(chip.IsEffectivelyEnabled);

        input.Receive(new NoteOffEvent((SevenBitNumber)43, (SevenBitNumber)0) { Channel = (FourBitNumber)(vm.Settings.Midi.BassChannel - 1) });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(keyboard.T8KeyNote);

        // Toggled off: keys still light up but don't change steps.
        chip.IsChecked = false;
        input = _ports.OpenedInputs.Last(); // the input is reopened with the new setting
        var next = vm.Bass.SelectedStep!;
        var noteBefore = next.Note;
        input.Receive(new NoteOnEvent((SevenBitNumber)52, (SevenBitNumber)100) { Channel = (FourBitNumber)(vm.Settings.Midi.BassChannel - 1) });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(noteBefore, next.Note);
        Assert.Equal(52, keyboard.T8KeyNote);
        window.Close();
    }

    [Fact]
    public void Notes_from_the_t8_sequencer_light_up_but_are_not_recorded()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        var before = vm.Bass.Steps[0].Note;

        vm.OnT8BassNote(50, pressed: true, fromSequencer: true);

        Assert.Equal(before, vm.Bass.Steps[0].Note);
        Assert.Same(vm.Bass.Steps[0], vm.Bass.SelectedStep);
        Assert.Equal(50, vm.Bass.T8KeyNote);
    }

    [Fact]
    public void A_velocity_0_note_only_releases_the_key()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = vm.Bass.Steps[0];
        var input = _ports.OpenedInputs.Last();
        var bass = (FourBitNumber)(vm.Settings.Midi.BassChannel - 1);

        input.Receive(new NoteOnEvent((SevenBitNumber)40, (SevenBitNumber)0) { Channel = bass });
        vm.OnT8BassNote(40, pressed: false);
        Assert.Same(vm.Bass.Steps[0], vm.Bass.SelectedStep); // nothing entered
        Assert.Null(vm.Bass.T8KeyNote);
    }

    [Fact]
    public void The_on_screen_keyboard_still_works_with_t8_keys_on()
    {
        using var vm = Create();
        vm.Bass.SelectedStep = vm.Bass.Steps[4];
        vm.Bass.EnterNoteCommand.Execute(48);
        Assert.Equal(48, vm.Bass.Steps[4].Note);
        Assert.Same(vm.Bass.Steps[5], vm.Bass.SelectedStep);
        Assert.Null(vm.Bass.T8KeyNote);
    }

    [AvaloniaFact]
    public void The_t8_keys_chip_is_greyed_out_without_a_t8_input()
    {
        var ports = FakePortProvider.WithT8();
        ports.Inputs = []; // the T-8 has no MIDI input here
        using var vm = new MainWindowViewModel(ports, new FakeDialogService(), new MemorySettingsStore(), () => []);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = MainWindowViewModel.BassTab;
        window.UpdateLayout();

        Assert.False(vm.T8InputOpen);
        var chip = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(t => t.Name == "NotesFromT8Toggle");
        Assert.False(chip.IsEffectivelyEnabled);
        Assert.Contains("need the T-8's MIDI input", (string)ToolTip.GetTip(chip)!);
        Assert.Contains("T-8 KEYS", chip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        window.Close();
    }
}
