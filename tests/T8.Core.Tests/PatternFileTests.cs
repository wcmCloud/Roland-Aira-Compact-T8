using T8.Core.Patterns;

namespace T8.Core.Tests;

public class PatternFileTests
{
    private static readonly string BackupRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "BACKUP");

    public static TheoryData<string> BassFiles() =>
        new(Directory.GetFiles(Path.Combine(BackupRoot, "BASS"), "*.PRM", SearchOption.AllDirectories));

    public static TheoryData<string> RhythmFiles() =>
        new(Directory.GetFiles(Path.Combine(BackupRoot, "RHYTHM"), "*.PRM", SearchOption.AllDirectories));

    [Theory]
    [MemberData(nameof(BassFiles))]
    public void Bass_round_trip_is_byte_identical(string path)
    {
        var original = File.ReadAllText(path);
        var written = BassPattern.Load(path).ToPrm().ToString();
        Assert.Equal(original, written);
    }

    [Theory]
    [MemberData(nameof(RhythmFiles))]
    public void Rhythm_round_trip_is_byte_identical(string path)
    {
        var original = File.ReadAllText(path);
        var written = RhythmPattern.Load(path).ToPrm().ToString();
        Assert.Equal(original, written);
    }

    [Fact]
    public void Library_loads_all_64_patterns()
    {
        var library = PatternLibrary.Load(BackupRoot);
        Assert.Equal(12, library.Bass[0, 0].Length);
        Assert.Equal(32, library.Rhythm[0, 0].Length);
    }

    [Fact]
    public void Decodes_bass_step()
    {
        var pattern = BassPattern.Load(PatternLibrary.BassPath(BackupRoot, 1, 1));
        var step4 = pattern.Steps[3];
        Assert.Equal(BassStepState.Note, step4.State);
        Assert.Equal(48, step4.Note);
        Assert.True(step4.Accent);
        Assert.True(step4.Slide);
        Assert.Equal(BassStepState.Tie, pattern.Steps[11].State);
    }

    [Fact]
    public void Decodes_rhythm_cell()
    {
        var pattern = RhythmPattern.Load(PatternLibrary.RhythmPath(BackupRoot, 1, 1));
        var hat = pattern.Steps[1][RhythmTrack.CH]; // "1405A"
        Assert.True(hat.On);
        Assert.Equal(4, hat.Velocity);
        Assert.Equal(SubStep.Off, hat.SubStep);
        Assert.Equal(5, hat.Probability);
        Assert.Equal(10, hat.SubStepProbability);
    }

    [Fact]
    public void Save_writes_library_that_reloads_identically()
    {
        var library = PatternLibrary.Load(BackupRoot);
        library.Bass[0, 0].Steps[0].Note = 40;
        library.Rhythm[0, 0].Steps[0][RhythmTrack.BD].SubStep = SubStep.Third;

        var target = Directory.CreateTempSubdirectory("t8-restore-").FullName;
        try
        {
            library.Save(target);
            var reloaded = PatternLibrary.Load(target);
            Assert.Equal(40, reloaded.Bass[0, 0].Steps[0].Note);
            Assert.Equal(SubStep.Third, reloaded.Rhythm[0, 0].Steps[0][RhythmTrack.BD].SubStep);
            Assert.Equal("172AA", reloaded.Rhythm[0, 0].Steps[0][RhythmTrack.BD].ToString());
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }
}
