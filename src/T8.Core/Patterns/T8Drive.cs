namespace T8.Core.Patterns;

/// <summary>
/// The T-8 in USB storage mode (hold PLAY while switching on). It mounts as a drive named "T-8"
/// with a BACKUP folder (the current patterns) and a RESTORE folder (patterns to load).
/// </summary>
public static class T8Drive
{
    public const string VolumeName = "T-8";
    public const string BackupFolder = "BACKUP";
    public const string RestoreFolder = "RESTORE";

    /// <summary>Places a mounted T-8 drive can appear on this OS.</summary>
    public static IEnumerable<string> CandidateRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                    yield return drive.RootDirectory.FullName;
            }
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine("/Volumes", VolumeName);
            yield break;
        }

        var user = Environment.UserName;
        foreach (var parent in new[] { $"/media/{user}", $"/run/media/{user}", "/media", "/mnt" })
            yield return Path.Combine(parent, VolumeName);
    }

    /// <summary>The first candidate that looks like a T-8 drive, or null.</summary>
    public static string? Find(IEnumerable<string>? candidates = null) =>
        (candidates ?? CandidateRoots()).FirstOrDefault(IsT8Drive);

    public static bool IsT8Drive(string root) =>
        Directory.Exists(Path.Combine(root, BackupFolder, "BASS")) &&
        Directory.Exists(Path.Combine(root, BackupFolder, "RHYTHM"));

    public static string BackupPath(string root) => Path.Combine(root, BackupFolder);

    public static string RestorePath(string root) => Path.Combine(root, RestoreFolder);

    /// <summary>
    /// Writes all 64 patterns into RESTORE. Afterwards eject the drive and press ACCENT on the T-8.
    /// Returns the RESTORE folder.
    /// </summary>
    public static string WriteRestore(PatternLibrary library, string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"The T-8 drive isn't at {root}.");
        var restore = RestorePath(root);
        Directory.CreateDirectory(restore);
        library.Save(restore);
        return restore;
    }
}
