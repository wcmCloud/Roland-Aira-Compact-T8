using T8.Core.Patterns;
using T8.Core.Songs;

namespace T8.Core.Tests;

public class SongTests
{
    private static Song Sample() => new()
    {
        Title = "Hardcore",
        Bpm = 160,
        Loop = true,
        Entries =
        [
            new SongEntry { Bank = 1, Pattern = 1, Repeats = 1, Name = "Intro", Color = "#5B8DEF", Fill = new SongFill(1, 16) },
            new SongEntry { Bank = 1, Pattern = 3, Repeats = 4, Name = "Main", Color = "#FF5C1A" },
            new SongEntry { Bank = 2, Pattern = 7, Repeats = 1, Name = "Outro" },
        ],
    };

    [Fact]
    public void Saves_and_loads_the_same_song()
    {
        var song = Sample();
        var json = song.ToJson();

        Assert.Contains("\"format\": \"t8song\"", json);
        Assert.Contains("\"version\": 1", json);
        var loaded = Song.FromJson(json);
        Assert.Equal(json, loaded.ToJson());
        Assert.Equal(new SongFill(1, 16), loaded.Entries[0].Fill);
        Assert.Null(loaded.Entries[1].Fill);
    }

    [Fact]
    public void Writes_and_reads_a_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"song-{Guid.NewGuid():N}{Song.FileExtension}");
        try
        {
            Sample().Save(path);
            Assert.Equal("Hardcore", Song.Load(path).Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("not json", "Not a song file")]
    [InlineData("{\"format\":\"other\",\"version\":1,\"title\":\"x\",\"loop\":false,\"entries\":[]}", "Not a T-8 song file")]
    [InlineData("{\"format\":\"t8song\",\"version\":9,\"title\":\"x\",\"loop\":false,\"entries\":[]}", "version 9")]
    [InlineData("{\"format\":\"t8song\",\"version\":1,\"title\":\"x\",\"loop\":false,\"entries\":[{\"bank\":5,\"pattern\":1,\"repeats\":1,\"name\":\"\",\"color\":\"#FFFFFF\"}]}", "Entry 1: bank must be 1–4.")]
    [InlineData("{\"format\":\"t8song\",\"version\":1,\"title\":\"x\",\"loop\":false,\"entries\":[{\"bank\":1,\"pattern\":1,\"repeats\":0,\"name\":\"\",\"color\":\"#FFFFFF\"}]}", "repeats must be 1–99")]
    [InlineData("{\"format\":\"t8song\",\"version\":1,\"title\":\"x\",\"loop\":false,\"entries\":[{\"bank\":1,\"pattern\":1,\"repeats\":1,\"name\":\"\",\"color\":\"red\"}]}", "colour")]
    public void Rejects_files_that_are_not_valid_songs(string json, string message)
    {
        var ex = Assert.Throws<FormatException>(() => Song.FromJson(json));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Plan_lays_out_every_repeat_and_fill_with_the_pattern_lengths()
    {
        var library = PatternLibrary.CreateBlank();
        library.Rhythm[0, 0].Length = 16;  // intro
        library.Rhythm[0, 15].Length = 8;  // fill
        library.Rhythm[0, 2].Length = 32;  // main
        library.Rhythm[1, 6].Length = 12;  // outro

        var plan = SongPlan.Create(Sample(), library);

        Assert.Equal(
            [(0, 1, 1, 1, 0), (0, 0, 1, 16, 16), (1, 1, 1, 3, 24), (1, 2, 1, 3, 56), (1, 3, 1, 3, 88), (1, 4, 1, 3, 120), (2, 1, 2, 7, 152)],
            plan.Passes.Select(p => (p.Entry, p.Repeat, p.Bank, p.Pattern, p.StartStep)));
        Assert.Equal(164, plan.TotalSteps);
        Assert.True(plan.Passes[1].IsFill);

        Assert.Equal(0, plan.PassAt(15));
        Assert.Equal(1, plan.PassAt(16));
        Assert.Equal(6, plan.PassAt(163));
        Assert.Equal(0, plan.PassAt(164)); // wraps for a looping song
        Assert.Equal(2, plan.FirstPassOf(1));
    }

    [Fact]
    public void Plan_for_one_entry_only_has_its_passes()
    {
        var plan = SongPlan.Create(Sample(), PatternLibrary.CreateBlank(), onlyEntry: 1);
        Assert.Equal(4, plan.Passes.Count);
        Assert.All(plan.Passes, p => Assert.Equal(1, p.Entry));
        Assert.Equal(0, plan.Passes[0].StartStep);
        Assert.Equal(-1, plan.FirstPassOf(0));
    }

    [Fact]
    public void Empty_song_has_an_empty_plan()
    {
        var plan = SongPlan.Create(new Song(), PatternLibrary.CreateBlank());
        Assert.Equal(0, plan.TotalSteps);
        Assert.Equal(-1, plan.PassAt(3));
    }
}
