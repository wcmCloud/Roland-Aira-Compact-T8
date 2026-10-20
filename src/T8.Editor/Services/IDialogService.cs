namespace T8.Editor.Services;

/// <summary>File and confirmation dialogs, behind an interface so view models can be tested.</summary>
public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);

    Task<bool> ConfirmAsync(string title, string message);
}
