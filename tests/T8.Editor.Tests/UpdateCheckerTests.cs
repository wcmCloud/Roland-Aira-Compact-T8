using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

/// <summary>Answers HTTP requests from a function, and records them.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    public static FakeHttpHandler Release(string tag, bool prerelease = false, bool draft = false) =>
        Json($$"""{"tag_name":"{{tag}}","html_url":"https://github.com/wcmCloud/Roland-Aira-Compact-T8/releases/tag/{{tag}}","prerelease":{{(prerelease ? "true" : "false")}},"draft":{{(draft ? "true" : "false")}}}""");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return answer(request, cancellationToken);
    }
}

internal sealed class FakeUrlOpener : IUrlOpener
{
    public List<string> Opened { get; } = new();

    public void Open(string url) => Opened.Add(url);
}

public class UpdateCheckerTests
{
    private static readonly ReleaseVersion Current = new(1, 0, 5);
    private DateTimeOffset _now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private UpdateChecker Checker(FakeHttpHandler handler) => new(new HttpClient(handler), Current, () => _now);

    [Theory]
    [InlineData("v1.0.6", 1, 0, 6, null)]
    [InlineData("1.0.6", 1, 0, 6, null)]
    [InlineData("v2.1", 2, 1, 0, null)]
    [InlineData("v1.1.0-beta.2", 1, 1, 0, "beta.2")]
    [InlineData("v1.0.6+build.7", 1, 0, 6, null)]
    public void Parses_release_tags(string tag, int major, int minor, int patch, string? pre)
    {
        Assert.Equal(new ReleaseVersion(major, minor, patch, pre), ReleaseVersion.Parse(tag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1")]
    [InlineData("v1.0.x")]
    [InlineData("v1.0.6-")]
    [InlineData("1.0.0.1")]
    public void Rejects_invalid_tags(string tag) => Assert.Null(ReleaseVersion.Parse(tag));

    [Fact]
    public void Compares_versions_semantically()
    {
        Assert.True(ReleaseVersion.Parse("1.0.10")!.CompareTo(ReleaseVersion.Parse("1.0.9")) > 0); // not as text
        Assert.True(ReleaseVersion.Parse("2.0.0")!.CompareTo(ReleaseVersion.Parse("1.9.9")) > 0);
        Assert.True(ReleaseVersion.Parse("1.1.0-beta")!.CompareTo(ReleaseVersion.Parse("1.1.0")) < 0);
        Assert.Equal(0, ReleaseVersion.Parse("v1.0.5")!.CompareTo(Current));
    }

    [Fact]
    public async Task A_newer_release_is_reported_with_its_page()
    {
        var handler = FakeHttpHandler.Release("v1.0.6");
        var result = await Checker(handler).CheckAsync(new UpdateCheckCache(), useCache: true);

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.Equal(new ReleaseVersion(1, 0, 6), result.Release!.Version);
        Assert.Equal("https://github.com/wcmCloud/Roland-Aira-Compact-T8/releases/tag/v1.0.6", result.Release.Url);

        // A plain GET to the public endpoint, with a User-Agent and nothing personal.
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(UpdateChecker.LatestReleaseUrl, request.RequestUri!.ToString());
        Assert.Equal("T8Editor/1.0.5", request.Headers.UserAgent.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.Null(request.Content);
        Assert.Empty(request.Headers.Select(h => h.Key).Except(["User-Agent", "Accept"]));
    }

    [Theory]
    [InlineData("v1.0.5")]
    [InlineData("v1.0.4")]
    [InlineData("v0.9.0")]
    public async Task The_same_or_an_older_release_means_up_to_date(string tag)
    {
        var result = await Checker(FakeHttpHandler.Release(tag)).CheckAsync(new UpdateCheckCache(), useCache: true);
        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task Prereleases_and_drafts_are_ignored()
    {
        Assert.Equal(UpdateOutcome.UpToDate, (await Checker(FakeHttpHandler.Release("v2.0.0", prerelease: true)).CheckAsync(new(), false)).Outcome);
        Assert.Equal(UpdateOutcome.UpToDate, (await Checker(FakeHttpHandler.Release("v2.0.0", draft: true)).CheckAsync(new(), false)).Outcome);
        Assert.Equal(UpdateOutcome.UpToDate, (await Checker(FakeHttpHandler.Release("v2.0.0-rc.1")).CheckAsync(new(), false)).Outcome);
    }

    [Fact]
    public async Task Bad_answers_fail_with_a_reason()
    {
        Assert.Contains("unknown version", (await Checker(FakeHttpHandler.Release("nightly")).CheckAsync(new(), false)).Error);
        Assert.Contains("unexpected", (await Checker(FakeHttpHandler.Json("not json")).CheckAsync(new(), false)).Error);
        Assert.Contains("unexpected", (await Checker(FakeHttpHandler.Json("""{"name":"x"}""")).CheckAsync(new(), false)).Error);
        Assert.Contains("limiting", (await Checker(FakeHttpHandler.Json("{}", HttpStatusCode.Forbidden)).CheckAsync(new(), false)).Error);
        var offline = new FakeHttpHandler((_, _) => throw new HttpRequestException("offline"));
        Assert.Contains("no connection", (await Checker(offline).CheckAsync(new(), false)).Error);
    }

    [Fact]
    public async Task A_slow_answer_times_out()
    {
        var slow = new FakeHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var checker = new UpdateChecker(new HttpClient(slow), Current, () => _now) { Timeout = TimeSpan.FromMilliseconds(100) };
        var result = await checker.CheckAsync(new UpdateCheckCache(), useCache: false);
        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        Assert.Contains("in time", result.Error);
    }

    [Fact]
    public async Task The_answer_is_cached_for_a_day()
    {
        var handler = FakeHttpHandler.Release("v1.0.6");
        var checker = Checker(handler);
        var cache = new UpdateCheckCache();

        await checker.CheckAsync(cache, useCache: true);
        Assert.Equal("v1.0.6", cache.LatestTag);
        Assert.Equal(_now, cache.CheckedAt);

        _now = _now.AddHours(23);
        var cached = await checker.CheckAsync(cache, useCache: true);
        Assert.True(cached.FromCache);
        Assert.Equal(UpdateOutcome.UpdateAvailable, cached.Outcome);
        Assert.Single(handler.Requests);

        await checker.CheckAsync(cache, useCache: false); // a manual check always asks
        Assert.Equal(2, handler.Requests.Count);

        _now = _now.AddHours(25);
        await checker.CheckAsync(cache, useCache: true);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task A_failed_check_does_not_touch_the_cache()
    {
        var cache = new UpdateCheckCache { CheckedAt = _now.AddDays(-3), LatestTag = "v1.0.5" };
        await Checker(FakeHttpHandler.Json("oops")).CheckAsync(cache, useCache: true);
        Assert.Equal(_now.AddDays(-3), cache.CheckedAt);
    }

    // ---- In the editor ----

    private static MainWindowViewModel Editor(FakeHttpHandler handler, MemorySettingsStore store, FakeUrlOpener opener, FakeDialogService? dialogs = null) =>
        new(FakePortProvider.WithT8(), dialogs ?? new FakeDialogService(), store, () => [],
            new UpdateChecker(new HttpClient(handler), Current), opener);

    [Fact]
    public async Task On_startup_a_newer_version_shows_the_chip_and_the_chip_opens_the_release()
    {
        var store = new MemorySettingsStore();
        var opener = new FakeUrlOpener();
        using var vm = Editor(FakeHttpHandler.Release("v1.0.6"), store, opener);
        await vm.StartupUpdateCheck;

        Assert.True(vm.HasUpdate);
        Assert.Equal("v1.0.6 available", vm.UpdateLabel);
        Assert.Contains("1.0.6", vm.UpdateTip);
        Assert.Equal("v1.0.6", store.Stored.UpdateCheck.LatestTag); // cached

        vm.OpenUpdatePageCommand.Execute(null);
        Assert.Equal(["https://github.com/wcmCloud/Roland-Aira-Compact-T8/releases/tag/v1.0.6"], opener.Opened);
    }

    [Fact]
    public async Task The_startup_check_is_silent_when_it_fails_and_off_when_turned_off()
    {
        var failing = new FakeHttpHandler((_, _) => throw new HttpRequestException("offline"));
        using (var vm = Editor(failing, new MemorySettingsStore(), new FakeUrlOpener()))
        {
            var status = vm.Status;
            await vm.StartupUpdateCheck;
            Assert.False(vm.HasUpdate);
            Assert.Equal(status, vm.Status);
        }

        var store = new MemorySettingsStore { Stored = new EditorSettings { CheckForUpdatesOnStartup = false } };
        var handler = FakeHttpHandler.Release("v1.0.6");
        using (var vm = Editor(handler, store, new FakeUrlOpener()))
        {
            await vm.StartupUpdateCheck;
            Assert.Empty(handler.Requests);
            Assert.False(vm.HasUpdate);
        }
    }

    [Fact]
    public async Task Manual_check_says_up_to_date_available_or_why_it_failed()
    {
        var store = new MemorySettingsStore { Stored = new EditorSettings { CheckForUpdatesOnStartup = false } };
        using (var vm = Editor(FakeHttpHandler.Release("v1.0.5"), store, new FakeUrlOpener()))
        {
            Assert.StartsWith("You're up to date", await vm.CheckForUpdatesNowAsync());
            Assert.StartsWith("You're up to date", vm.Status);
            Assert.False(vm.HasUpdate);
        }
        using (var vm = Editor(FakeHttpHandler.Release("v1.1.0"), store, new FakeUrlOpener()))
        {
            Assert.Contains("1.1.0 is available", await vm.CheckForUpdatesNowAsync());
            Assert.True(vm.HasUpdate);
        }
        using (var vm = Editor(FakeHttpHandler.Json("{}", HttpStatusCode.TooManyRequests), store, new FakeUrlOpener()))
            Assert.StartsWith("Couldn't check for updates: GitHub is limiting requests", await vm.CheckForUpdatesNowAsync());
    }

    [Fact]
    public async Task About_offers_the_check()
    {
        var dialogs = new FakeDialogService();
        var store = new MemorySettingsStore { Stored = new EditorSettings { CheckForUpdatesOnStartup = false } };
        using var vm = Editor(FakeHttpHandler.Release("v1.0.5"), store, new FakeUrlOpener(), dialogs);
        await vm.ShowAboutCommand.ExecuteAsync(null);
        Assert.StartsWith("You're up to date", await dialogs.AboutCheckForUpdates!());
    }

    [AvaloniaFact]
    public async Task The_chip_shows_in_the_status_bar_and_the_about_button_shows_the_answer()
    {
        var opener = new FakeUrlOpener();
        using var vm = Editor(FakeHttpHandler.Release("v1.0.6"), new MemorySettingsStore(), opener);
        await vm.StartupUpdateCheck;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        var chip = window.FindControl<Button>("UpdateChip")!;
        Assert.True(chip.IsEffectivelyVisible);
        Assert.Contains("lit", chip.Classes);
        chip.Command!.Execute(null);
        Assert.Single(opener.Opened);
        window.Close();

        var about = new AboutWindow { CheckForUpdates = () => Task.FromResult("You're up to date (v1.0.5 is the latest version).") };
        about.Show();
        Assert.True(about.FindControl<Button>("CheckUpdatesButton")!.IsEffectivelyVisible);
        await about.RunUpdateCheckAsync();
        Assert.StartsWith("You're up to date", about.FindControl<TextBlock>("UpdateResultText")!.Text);
        about.Close();
    }

    [AvaloniaFact]
    public void The_settings_window_has_the_startup_check_option()
    {
        var settings = new EditorSettings();
        Assert.True(settings.CheckForUpdatesOnStartup);
        var vm = new MidiSettingsViewModel(FakePortProvider.WithT8(), settings);
        var window = new MidiSettingsWindow { DataContext = vm };
        window.Show();
        var toggle = window.FindControl<ToggleSwitch>("CheckUpdatesSwitch")!;
        Assert.True(toggle.IsChecked);
        toggle.IsChecked = false;
        Assert.False(vm.ToSettings().CheckForUpdatesOnStartup);
        window.Close();
    }
}
