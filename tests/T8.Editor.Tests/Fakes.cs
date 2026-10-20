using T8.Editor.Services;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> Folders { get; } = new();
    public bool ConfirmResult { get; set; } = true;
    public int ConfirmCount { get; private set; }

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folders.Count > 0 ? Folders.Dequeue() : null);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK")
    {
        ConfirmCount++;
        LastConfirmText = confirmText;
        return Task.FromResult(ConfirmResult);
    }

    public string? LastConfirmText { get; private set; }

    /// <summary>Called with the settings window's view model; return true to press Save.</summary>
    public Func<MidiSettingsViewModel, bool> OnMidiSettings { get; set; } = _ => false;

    public Task<bool> ShowMidiSettingsAsync(MidiSettingsViewModel settings) => Task.FromResult(OnMidiSettings(settings));

    public Queue<string?> SongFiles { get; } = new();

    public Task<string?> PickSongToOpenAsync() => Task.FromResult(SongFiles.Count > 0 ? SongFiles.Dequeue() : null);

    public Task<string?> PickSongToSaveAsync(string suggestedName) => Task.FromResult(SongFiles.Count > 0 ? SongFiles.Dequeue() : null);

    public int AboutCount { get; private set; }

    public Task ShowAboutAsync()
    {
        AboutCount++;
        return Task.CompletedTask;
    }
}

internal sealed class MemorySettingsStore : ISettingsStore
{
    public EditorSettings Stored { get; set; } = new();
    public int SaveCount { get; private set; }

    public EditorSettings Load() => Stored.Clone();

    public void Save(EditorSettings settings)
    {
        SaveCount++;
        Stored = settings.Clone();
    }
}
