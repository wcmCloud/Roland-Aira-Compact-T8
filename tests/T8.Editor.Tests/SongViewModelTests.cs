using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Editor.ViewModels;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class SongViewModelTests : IDisposable
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public SongViewModelTests()
    {
        _vm = new MainWindowViewModel(_ports, _dialogs);
        _vm.OpenFolder(TestPaths.Backup);
    }

    public void Dispose() => _vm.Dispose();

    private SongViewModel Song => _vm.Song;

    [Fact]
    public void Adds_the_selected_pattern_with_a_name_and_colour()
    {
        _vm.SelectSlot(2, 5);
        Song.AddCurrentPatternCommand.Execute(null);

        var entry = Song.Entries.Single();
        Assert.Equal((2, 5, 1), (entry.Bank, entry.Pattern, entry.Repeats));
        Assert.Equal("Part 1", entry.Name);
        Assert.Equal(SongViewModel.Palette[0], entry.Color);
        Assert.Equal(_vm.Library.Rhythm[1, 4].Length, entry.Steps);
        Assert.True(Song.IsDirty);
        Assert.Equal(2, Song.LoopChoices.Count); // whole song + entry 1
    }

    [Fact]
    public void Entries_can_be_edited_moved_duplicated_and_removed()
    {
        var a = Song.AddEntry(1, 1);
        var b = Song.AddEntry(1, 2);
        a.Repeats = 4;
        a.Name = "Main";
        a.HasFill = true;
        Assert.Equal(new SongFill(1, 16), a.Model.Fill);
        a.FillPattern = 12;
        a.Bank = 3; // the fill follows the entry's bank
        Assert.Equal(new SongFill(3, 12), a.Model.Fill);

        b.MoveUpCommand.Execute(null);
        Assert.Same(b, Song.Entries[0]);
        Assert.Equal(1, b.Number);

        a.DuplicateCommand.Execute(null);
        Assert.Equal(3, Song.Entries.Count);
        Assert.Equal("Main", Song.Entries[2].Name);
        Assert.NotSame(a.Model, Song.Entries[2].Model);

        b.RemoveCommand.Execute(null);
        Assert.Equal(2, Song.Entries.Count);
        Assert.Equal(2, Song.Song.Entries.Count);

        var color = a.Color;
        a.NextColorCommand.Execute(null);
        Assert.NotEqual(color, a.Color);
        Assert.Equal(4, a.Repeats);
    }

    [Fact]
    public void Timeline_has_a_block_per_entry_and_per_fill_sharing_the_whole_song()
    {
        var a = Song.AddEntry(1, 1);
        Song.AddEntry(1, 2);
        a.HasFill = true;

        Assert.Equal(3, Song.Timeline.Count);
        Assert.True(Song.Timeline[1].IsFill);
        Assert.Equal(1, Song.Timeline.Sum(b => b.Share), precision: 6);
    }

    [Fact]
    public void Saves_and_opens_a_song_file()
    {
        Song.Title = "My song";
        Song.AddEntry(1, 3).Repeats = 2;
        using var temp = TestPaths.CreateTempFolder();
        var path = Path.Combine(temp.Path, "my.t8song");
        _dialogs.SongFiles.Enqueue(path);

        Song.SaveSongAsCommand.Execute(null);
        Assert.False(Song.IsDirty);
        Assert.Equal("my.t8song", Song.FileLabel);
        Assert.Equal(_vm.Bpm, T8.Core.Songs.Song.Load(path).Bpm);

        Song.NewSongCommand.Execute(null);
        Assert.Empty(Song.Entries);
        _dialogs.SongFiles.Enqueue(path);
        Song.OpenSongCommand.Execute(null);

        Assert.Equal("My song", Song.Title);
        Assert.Equal(2, Song.Entries.Single().Repeats);
        Assert.Contains("Opened song", _vm.Status);
    }

    [Fact]
    public void A_broken_song_file_is_reported()
    {
        using var temp = TestPaths.CreateTempFolder();
        var path = Path.Combine(temp.Path, "bad.t8song");
        File.WriteAllText(path, "{}");
        Assert.False(Song.OpenSong(path));
        Assert.Contains("Couldn't open the song", _vm.Status);
    }

    [Fact]
    public void Play_and_stop_the_song_through_the_t8()
    {
        Assert.False(Song.PlayCommand.CanExecute(null)); // nothing to play
        Song.AddEntry(1, 1);
        Song.AddEntry(1, 2);

        Song.PlayCommand.Execute(null);
        Assert.True(Song.IsPlaying);
        Assert.Contains("Playing", _vm.Status);
        Song.UpdatePosition();
        Assert.True(Song.Entries[0].IsCurrent);
        Assert.NotEqual("—", Song.CurrentText);

        Song.StopCommand.Execute(null);
        Assert.False(Song.IsPlaying);
        Assert.False(Song.Entries[0].IsCurrent);
    }

    [Fact]
    public void Playing_a_pattern_stops_the_song_and_the_other_way_round()
    {
        Song.AddEntry(1, 1);
        Song.PlayCommand.Execute(null);
        _vm.PlayCommand.Execute(null);
        Assert.False(Song.IsPlaying);
        Assert.True(_vm.IsPlaying);

        Song.PlayCommand.Execute(null);
        Assert.True(Song.IsPlaying);
        Assert.False(_vm.IsPlaying);
        Song.StopCommand.Execute(null);
    }

    [Fact]
    public void Next_jumps_to_the_next_entry()
    {
        Song.AddEntry(1, 1);
        Song.AddEntry(1, 2);
        Song.PlayCommand.Execute(null);
        Thread.Sleep(30);

        Song.NextCommand.Execute(null);
        Thread.Sleep(30);
        Song.UpdatePosition();

        Assert.True(Song.Entries[1].IsCurrent);
        Song.StopCommand.Execute(null);
    }

    [Fact]
    public void Loop_choices_follow_the_entries()
    {
        Song.AddEntry(1, 1).Name = "Intro";
        Assert.Equal("Whole song", Song.LoopChoices[0].Name);
        Assert.Contains("Intro", Song.LoopChoices[1].Name);
        Song.LoopChoice = Song.LoopChoices[1];
        Song.PlayCommand.Execute(null);
        Assert.True(Song.IsPlaying);
        Song.StopCommand.Execute(null);
    }
}
