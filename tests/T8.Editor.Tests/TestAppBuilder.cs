using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(T8.Editor.Tests.TestAppBuilder))]

namespace T8.Editor.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
