using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

/// <summary>
/// Renders the screenshots in docs/screenshots for the README, at a MacBook Air 13" window size
/// (1470 points wide) and Retina 2× scale. Runs only when T8_DOCS_DIR is set:
/// <c>T8_DOCS_DIR=$PWD/docs/screenshots dotnet test tests/T8.Editor.Tests --filter DocsScreenshots</c>.
/// </summary>
public class DocsScreenshots
{
    private const double RenderScale = 2;
    private static readonly string? Output = Environment.GetEnvironmentVariable("T8_DOCS_DIR");

    [AvaloniaFact]
    public void Render_readme_screenshots()
    {
        if (Output is null)
            return;
        Directory.CreateDirectory(Output);

        var settings = new EditorSettings { OutputPortName = "T-8 MIDI IN" };
        settings.Midi.BassChannel = 6;
        settings.Midi.RhythmChannel = 5;
        settings.Midi.ProgramChangeChannel = 7;
        var ports = new FakePortProvider(new T8.Midi.Ports.MidiPortInfo("T-8 MIDI IN", "T-8 MIDI IN"));
        // The test data folder holds a BACKUP folder like the T-8 drive, so the drive shows as connected.
        var vm = new MainWindowViewModel(ports, new FakeDialogService(), new MemorySettingsStore(settings),
            () => [Path.GetDirectoryName(TestPaths.Backup)!]);
        vm.OpenFolder(TestPaths.Backup);
        vm.Status = "Loaded 64 patterns from BACKUP.";
        var window = new MainWindow { DataContext = vm, Width = 1470, Height = 890 };
        window.Show();

        // Rhythm tab: a techno pattern, step 5 playing, a closed hat with a roll in the inspector.
        vm.RhythmGenerator.Style = RhythmGeneratorViewModel.Styles.Single(s => s.Value == RhythmStyle.Techno);
        vm.RhythmGenerator.Seed = 7;
        vm.RhythmGenerator.GenerateCommand.Execute(null);
        var hat = vm.Rhythm[RhythmTrack.CH].Cells.First(c => c.On && c.StepNumber > 8);
        hat.SubStep = SubStep.Third;
        hat.ActivateCommand.Execute(null);
        vm.Rhythm.ShowPlayingStep(4);
        vm.Rhythm.CopyTrackCommand.Execute(vm.Rhythm[RhythmTrack.CH]); // the closed hat row selected and copied
        vm.Status = "Techno drums · seed 7 · 32 steps";
        Settle(window);
        Save(window, "main-rhythm.png");
        Crop(window, window.FindControl<Border>("PatternsCard")!, "patterns.png");
        Crop(window, window.FindControl<Border>("PlaybackCard")!, "playback.png");
        Crop(window, Find<RhythmEditorView>(window), "rhythm-editor.png");
        Crop(window, Find<RhythmGeneratorView>(window), "rhythm-generator.png");

        // Several active closed hats picked to set them together.
        foreach (var cell in vm.Rhythm[RhythmTrack.CH].Cells.Where(c => c.On).Take(6))
            vm.Rhythm.ToggleMultiSelect(cell);
        vm.Rhythm.BatchVelocity = 5;
        vm.Rhythm.BatchSubStep = SubStep.Half;
        Settle(window);
        Crop(window, Find<RhythmEditorView>(window), "rhythm-multiselect.png");
        vm.Rhythm.ClearMultiSelectionCommand.Execute(null);

        // The fill pattern made from it.
        vm.RhythmGenerator.MakeFillCommand.Execute(null);
        vm.SelectSlot(1, 16);
        vm.Rhythm.ShowPlayingStep(null);
        Settle(window);
        Crop(window, Find<RhythmEditorView>(window), "fill-pattern.png");
        vm.SelectSlot(1, 1);

        // Bass tab: an E minor pentatonic line over i – VI – III – VII.
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
        var bass = vm.BassGenerator;
        bass.Key = BassGeneratorViewModel.Keys[4];
        bass.Scale = BassGeneratorViewModel.Scales.Single(s => s.Value == Scale.MinorPentatonic);
        bass.Progression = BassGeneratorViewModel.Progressions.Single(p => p.Value == Progression.OneSixThreeSeven);
        bass.StepsPerChord = 4;
        bass.Seed = 12;
        vm.Bass.Length = 16;
        bass.GenerateCommand.Execute(null);
        vm.Bass.Steps[2].SelectCommand.Execute(null);
        vm.Bass.ShowPlayingStep(6);
        vm.Status = bass.Summary;
        Settle(window);
        Save(window, "main-bass.png");
        Crop(window, Find<BassEditorView>(window), "bass-editor.png");
        Crop(window, Find<BassGeneratorView>(window), "bass-generator.png");

        // Right-click menu of a bass step, with the octave 2 notes open.
        var stepControl = Find<BassEditorView>(window).GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("bassStep") && b.DataContext == vm.Bass.Steps[2]);
        var menu = BassEditorView.BuildStepMenu(vm.Bass.Steps[2]);
        menu.Open(stepControl);
        Settle(window);
        var octave = menu.ItemsSource!.OfType<MenuItem>().First(m => (string)m.Header! == "Octave 2");
        octave.IsSubMenuOpen = true;
        Settle(window);
        var subMenu = octave.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().FirstOrDefault()?.Child;
        // Headless popups are drawn in the window (at its corner), so crop just the menus.
        var area = Bounds(window, menu);
        if (subMenu is not null)
            area = area.Union(Bounds(window, subMenu));
        CropRect(window, area, "bass-step-menu.png");
        menu.Close();

        // Song tab: "Blues", seven parts with repeats and fills, the second verse playing.
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 2;
        var song = vm.Song;
        song.Title = "Blues";
        var parts = new (int Pattern, string Name, int Repeats, bool Fill)[]
        {
            (1, "Intro", 1, true),
            (2, "Verse 1", 4, false),
            (3, "Turnaround", 1, false),
            (2, "Verse 2", 4, true),
            (5, "Bridge", 2, false),
            (6, "Solo", 4, true),
            (7, "Outro", 1, false),
        };
        foreach (var (pattern, name, repeats, fill) in parts)
        {
            var entry = song.AddEntry(1, pattern);
            entry.Name = name;
            entry.Repeats = repeats;
            entry.HasFill = fill;
        }
        song.LoopChoice = song.LoopChoices.Single(c => c.Value == SongViewModel.LoopSong);
        song.Entries[3].IsCurrent = true; // Verse 2, third time round
        foreach (var block in song.Timeline)
            block.Progress = block.Entry < 3 ? 1 : block.Entry == 3 && !block.IsFill ? 0.62 : 0;
        song.CurrentText = "1-2 Verse 2  (3/4)";
        song.NextText = "1-2 Verse 2";
        song.PositionText = "Step 9 / 16 · 120 BPM";
        song.TimeText = "01:18";
        song.EntryProgress = 0.55;
        song.SongProgress = 0.36;
        vm.Status = "Playing \"Blues\" through the T-8's sounds.";
        Settle(window);
        Save(window, "song-blues.png");
        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 0;

        // MIDI settings after detecting the T-8's channels.
        var settingsVm = new MidiSettingsViewModel(ports, vm.Settings)
        {
            DetectResult = "Detected bass 6, rhythm 5, pattern select 7. Press Save to keep them.",
        };
        var settingsWindow = new MidiSettingsWindow { DataContext = settingsVm };
        settingsWindow.Show();
        Settle(settingsWindow);
        Save(settingsWindow, "midi-settings.png");
        settingsWindow.Close();

        var about = new AboutWindow();
        about.Show();
        Settle(about);
        Save(about, "about.png");

        var help = new HelpWindow();
        help.ShowTopic(T8.Editor.Help.HelpTopics.RhythmGenerator);
        help.Show();
        Settle(help);
        Save(help, "help.png");
        Assert.Contains("Christos Christodoulidis", about.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        about.Close();

        window.Close();
        vm.Dispose();
    }

    private static T Find<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>().First();

    private static void Settle(TopLevel window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Visual visual, string name)
    {
        using var bitmap = Render(visual);
        bitmap.Save(Path.Combine(Output!, name));
    }

    /// <summary>Saves the part of the window a control covers, so it keeps the window's background.</summary>
    private static void Crop(Window window, Visual part, string name) => CropRect(window, Bounds(window, part), name);

    private static Rect Bounds(Window window, Visual part) =>
        new(part.TranslatePoint(new Point(0, 0), window)!.Value, part.Bounds.Size);

    private static void CropRect(Window window, Rect area, string name)
    {
        using var full = Render(window);
        var source = new Rect(area.X * RenderScale, area.Y * RenderScale, area.Width * RenderScale, area.Height * RenderScale);
        using var target = new RenderTargetBitmap(new PixelSize((int)source.Width, (int)source.Height), new Vector(96 * RenderScale, 96 * RenderScale));
        using (var ctx = target.CreateDrawingContext())
            ctx.DrawImage(full, source, new Rect(area.Size));
        target.Save(Path.Combine(Output!, name));
    }

    private static RenderTargetBitmap Render(Visual visual)
    {
        var size = visual.Bounds.Size;
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width * RenderScale), (int)Math.Ceiling(size.Height * RenderScale)),
            new Vector(96 * RenderScale, 96 * RenderScale));
        bitmap.Render(visual);
        return bitmap;
    }

    private sealed class MemorySettingsStore(EditorSettings settings) : ISettingsStore
    {
        public EditorSettings Load() => settings.Clone();
        public void Save(EditorSettings value) { }
    }
}
