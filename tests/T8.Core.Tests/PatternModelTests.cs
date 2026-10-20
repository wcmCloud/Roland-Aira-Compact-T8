using T8.Core.Patterns;
using T8.Core.Prm;
using T8.Tests.Shared;

namespace T8.Core.Tests;

public class PatternModelTests
{
    [Theory]
    [InlineData("000AA", false, 0, SubStep.Off, 10, 10)]
    [InlineData("170AA", true, 7, SubStep.Off, 10, 10)]
    [InlineData("1A0AA", true, 10, SubStep.Off, 10, 10)]
    [InlineData("1405A", true, 4, SubStep.Off, 5, 10)]
    [InlineData("171A4", true, 7, SubStep.Half, 10, 4)]
    [InlineData("1738A", true, 7, SubStep.Quarter, 8, 10)]
    [InlineData("144AA", true, 4, SubStep.Flam, 10, 10)]
    public void Rhythm_cell_codes_decode_and_encode(string code, bool on, int velocity, SubStep subStep, int probability, int subProbability)
    {
        var cell = RhythmCell.Parse(code);

        Assert.Equal(on, cell.On);
        Assert.Equal(velocity, cell.Velocity);
        Assert.Equal(subStep, cell.SubStep);
        Assert.Equal(probability, cell.Probability);
        Assert.Equal(subProbability, cell.SubStepProbability);
        Assert.Equal(code, cell.ToString());
    }

    [Theory]
    [InlineData("170A")]    // too short
    [InlineData("170AAA")]  // too long
    [InlineData("270AA")]   // on flag above 1
    [InlineData("175AA")]   // sub step above 4
    [InlineData("1B0AA")]   // not 0-9/A
    public void Invalid_rhythm_cell_codes_throw(string code) =>
        Assert.Throws<FormatException>(() => RhythmCell.Parse(code));

    [Fact]
    public void Rhythm_cell_rejects_out_of_range_values_on_write()
    {
        var cell = new RhythmCell { Velocity = 11 };
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.ToString());
    }

    [Fact]
    public void New_rhythm_cell_is_blank_like_the_t8() => Assert.Equal("000AA", new RhythmCell().ToString());

    [Fact]
    public void Cloned_cells_and_steps_are_independent()
    {
        var cell = new RhythmCell { On = true, Velocity = 5 };
        var copy = cell.Clone();
        copy.Velocity = 9;
        Assert.Equal(5, cell.Velocity);

        var step = new BassStep { Note = 40, Accent = true };
        var stepCopy = step.Clone();
        stepCopy.Note = 50;
        Assert.Equal(40, step.Note);
        Assert.True(stepCopy.Accent);
    }

    [Fact]
    public void Rhythm_step_indexer_maps_tracks()
    {
        var step = new RhythmStep();
        step[RhythmTrack.OH].On = true;
        Assert.True(step.Cells[7].On);
        Assert.Equal(RhythmPattern.TrackCount, step.Cells.Length);
    }

    [Fact]
    public void New_bass_pattern_writes_a_blank_t8_file()
    {
        var text = new BassPattern().ToPrm().ToString();
        Assert.StartsWith("LENGTH\t= 16\nTRIPLET\t= 0\nSTEP 1\t= STATE=0 NOTE=36 ACCENT=0 SLIDE=0\n", text);
        Assert.EndsWith("STEP 32\t= STATE=0 NOTE=36 ACCENT=0 SLIDE=0\n", text);
    }

    [Fact]
    public void New_rhythm_pattern_writes_a_blank_t8_file()
    {
        var text = new RhythmPattern().ToPrm().ToString();
        Assert.StartsWith("LENGTH\t= 16\nSCALE\t= 1\nSHUFFLE\t= 0\nFLAM\t= 36\n", text);
        Assert.Contains("STEP 32\t= AC=000AA BD=000AA SD=000AA LT=000AA HT=000AA CY=000AA CH=000AA OH=000AA\n", text);
    }

    [Fact]
    public void Unknown_bass_state_throws()
    {
        var doc = new BassPattern().ToPrm();
        var text = doc.ToString().Replace("STEP 3\t= STATE=0", "STEP 3\t= STATE=7");
        Assert.Throws<FormatException>(() => BassPattern.FromPrm(PrmDocument.Parse(text)));
    }

    [Fact]
    public void Bass_and_rhythm_save_to_disk()
    {
        using var temp = TestPaths.CreateTempFolder();
        var bass = new BassPattern { Length = 7 };
        bass.Steps[0].State = BassStepState.Note;
        bass.Steps[0].Note = 60;
        var rhythm = new RhythmPattern { Shuffle = 140 };
        rhythm.Steps[5][RhythmTrack.SD].On = true;

        var bassPath = Path.Combine(temp.Path, "b.PRM");
        var rhythmPath = Path.Combine(temp.Path, "r.PRM");
        bass.Save(bassPath);
        rhythm.Save(rhythmPath);

        var bassBack = BassPattern.Load(bassPath);
        var rhythmBack = RhythmPattern.Load(rhythmPath);
        Assert.Equal(7, bassBack.Length);
        Assert.Equal(60, bassBack.Steps[0].Note);
        Assert.Equal(140, rhythmBack.Shuffle);
        Assert.True(rhythmBack.Steps[5][RhythmTrack.SD].On);
    }

    [Fact]
    public void Library_paths_follow_t8_naming()
    {
        Assert.Equal(Path.Combine("root", "BASS", "PTN_BANK02", "T8_BASS_PTN02_09.PRM"), PatternLibrary.BassPath("root", 2, 9));
        Assert.Equal(Path.Combine("root", "RHYTHM", "PTN_BANK04", "T8_RHYTHM_PTN04_16.PRM"), PatternLibrary.RhythmPath("root", 4, 16));
    }

    [Fact]
    public void Library_load_fails_clearly_when_a_file_is_missing()
    {
        using var temp = TestPaths.CreateTempFolder();
        Assert.ThrowsAny<IOException>(() => PatternLibrary.Load(temp.Path));
    }
}
