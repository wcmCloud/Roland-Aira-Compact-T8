using T8.Editor.ViewModels;

namespace T8.Editor.Services;

/// <summary>File and confirmation dialogs, behind an interface so view models can be tested.</summary>
public interface IDialogService
{
    /// <summary>Asks for a folder. <paramref name="startFolder"/> is where the picker opens, when that folder exists.</summary>
    Task<string?> PickFolderAsync(string title, string? startFolder = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK");

    /// <summary>Shows the MIDI settings window. True when the user pressed Save.</summary>
    Task<bool> ShowMidiSettingsAsync(MidiSettingsViewModel settings);

    /// <summary>Shows the About window; <paramref name="checkForUpdates"/> runs its "Check for updates" button.</summary>
    Task ShowAboutAsync(Func<Task<string>>? checkForUpdates = null);

    /// <summary>Opens the help window at a topic (see <see cref="Help.HelpTopics"/>), or at the start.</summary>
    void ShowHelp(string? topic = null);

    /// <summary>Asks for a .t8song file to open; null if cancelled.</summary>
    Task<string?> PickSongToOpenAsync();

    /// <summary>Asks where to save a .t8song file; null if cancelled.</summary>
    Task<string?> PickSongToSaveAsync(string suggestedName);
}
