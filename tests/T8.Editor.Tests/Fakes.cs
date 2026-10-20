using T8.Editor.Services;

namespace T8.Editor.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> Folders { get; } = new();
    public bool ConfirmResult { get; set; } = true;
    public int ConfirmCount { get; private set; }

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folders.Count > 0 ? Folders.Dequeue() : null);

    public Task<bool> ConfirmAsync(string title, string message)
    {
        ConfirmCount++;
        return Task.FromResult(ConfirmResult);
    }
}
