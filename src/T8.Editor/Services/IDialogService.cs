using T8.Editor.ViewModels;

namespace T8.Editor.Services;

/// <summary>File and confirmation dialogs, behind an interface so view models can be tested.</summary>
public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);

    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK");

    /// <summary>Shows the MIDI settings window. True when the user pressed Save.</summary>
    Task<bool> ShowMidiSettingsAsync(MidiSettingsViewModel settings);

    /// <summary>Shows the About window.</summary>
    Task ShowAboutAsync();

    /// <summary>Asks for a .t8song file to open; null if cancelled.</summary>
    Task<string?> PickSongToOpenAsync();

    /// <summary>Asks where to save a .t8song file; null if cancelled.</summary>
    Task<string?> PickSongToSaveAsync(string suggestedName);
}
