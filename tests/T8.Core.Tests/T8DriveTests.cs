using T8.Core.Patterns;
using T8.Tests.Shared;

namespace T8.Core.Tests;

public class T8DriveTests
{
    private static string MakeDrive(string parent, string name = "T-8")
    {
        var root = Path.Combine(parent, name);
        PatternLibrary.Load(TestPaths.Backup).Save(Path.Combine(root, "BACKUP"));
        Directory.CreateDirectory(Path.Combine(root, "RESTORE"));
        return root;
    }

    [Fact]
    public void Recognises_a_t8_drive_by_its_backup_folder()
    {
        using var temp = TestPaths.CreateTempFolder();
        var drive = MakeDrive(temp.Path);

        Assert.True(T8Drive.IsT8Drive(drive));
        Assert.False(T8Drive.IsT8Drive(temp.Path));
    }

    [Fact]
    public void Find_returns_the_first_t8_drive()
    {
        using var temp = TestPaths.CreateTempFolder();
        var other = Directory.CreateDirectory(Path.Combine(temp.Path, "USB")).FullName;
        var drive = MakeDrive(temp.Path);

        Assert.Equal(drive, T8Drive.Find([Path.Combine(temp.Path, "missing"), other, drive]));
        Assert.Null(T8Drive.Find([other]));
    }

    [Fact]
    public void Candidate_roots_cover_this_platform()
    {
        var roots = T8Drive.CandidateRoots().ToList();
        Assert.NotEmpty(roots);
        if (OperatingSystem.IsLinux())
            Assert.Contains(Path.Combine("/media", Environment.UserName, "T-8"), roots);
        if (OperatingSystem.IsMacOS())
            Assert.Equal(["/Volumes/T-8"], roots);
    }

    [Fact]
    public void Write_restore_puts_all_patterns_in_the_restore_folder()
    {
        using var temp = TestPaths.CreateTempFolder();
        var drive = MakeDrive(temp.Path);
        var library = PatternLibrary.Load(T8Drive.BackupPath(drive));
        library.Bass[1, 1].Steps[0].Note = 60;

        var restore = T8Drive.WriteRestore(library, drive);

        Assert.Equal(Path.Combine(drive, "RESTORE"), restore);
        Assert.Equal(60, BassPattern.Load(PatternLibrary.BassPath(restore, 2, 2)).Steps[0].Note);
        Assert.Equal(64, Directory.GetFiles(Path.Combine(restore, "RHYTHM"), "*.PRM", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void Write_restore_creates_restore_folder_if_missing()
    {
        using var temp = TestPaths.CreateTempFolder();
        T8Drive.WriteRestore(PatternLibrary.CreateBlank(), temp.Path);
        Assert.True(File.Exists(PatternLibrary.BassPath(Path.Combine(temp.Path, "RESTORE"), 1, 1)));
    }

    [Fact]
    public void Write_restore_fails_when_the_drive_is_gone() =>
        Assert.Throws<DirectoryNotFoundException>(() =>
            T8Drive.WriteRestore(PatternLibrary.CreateBlank(), "/nonexistent/T-8"));
}
