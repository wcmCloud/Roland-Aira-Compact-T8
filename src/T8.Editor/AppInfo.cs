using System.Reflection;

namespace T8.Editor;

/// <summary>Name, version and author shown in the title bar and the About window.</summary>
public static class AppInfo
{
    public const string Name = "Roland Aira Compact T-8 Editor";
    public const string Author = "Christos Christodoulidis";
    public const string Website = "https://github.com/wcmCloud/Roland-Aira-Compact-T8";

    /// <summary>The version from the build (Version in the project file, or -p:Version when publishing).</summary>
    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0);

    /// <summary>"v1.0", or "v1.0.2" when there is a patch number.</summary>
    public static string DisplayVersion { get; } = Format(Version);

    /// <summary>The full version, e.g. "1.0.0" or "1.1.0-ci.42", without the commit hash.</summary>
    public static string FullVersion { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Version.ToString())
        .Split('+')[0];

    public static string Format(Version version) =>
        version.Build > 0 ? $"v{version.Major}.{version.Minor}.{version.Build}" : $"v{version.Major}.{version.Minor}";
}
