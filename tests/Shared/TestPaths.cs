namespace T8.Tests.Shared;

internal static class TestPaths
{
    /// <summary>The factory pattern backup copied next to each test assembly.</summary>
    public static string Backup => Path.Combine(AppContext.BaseDirectory, "TestData", "BACKUP");

    /// <summary>A fresh temporary folder, deleted when the returned handle is disposed.</summary>
    public static TempFolder CreateTempFolder() => new(Directory.CreateTempSubdirectory("t8-tests-").FullName);
}

internal sealed class TempFolder(string path) : IDisposable
{
    public string Path { get; } = path;

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
