using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using T8.Editor.ViewModels;
using T8.Editor.Views;

namespace T8.Editor.Services;

public sealed class AvaloniaDialogService(Window owner) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var yes = new Button { Content = confirmText, IsDefault = true };
        var no = new Button { Content = "Cancel", IsCancel = true };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    public Task ShowAboutAsync() => new AboutWindow().ShowDialog(owner);

    private static readonly FilePickerFileType SongFiles = new("T-8 song") { Patterns = ["*" + T8.Core.Songs.Song.FileExtension] };

    public async Task<string?> PickSongToOpenAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open song",
            AllowMultiple = false,
            FileTypeFilter = [SongFiles],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSongToSaveAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save song",
            SuggestedFileName = suggestedName + T8.Core.Songs.Song.FileExtension,
            DefaultExtension = T8.Core.Songs.Song.FileExtension.TrimStart('.'),
            FileTypeChoices = [SongFiles],
        });
        return file?.TryGetLocalPath();
    }

    public async Task<bool> ShowMidiSettingsAsync(MidiSettingsViewModel settings)
    {
        var window = new MidiSettingsWindow { DataContext = settings };
        return await window.ShowDialog<bool>(owner);
    }
}
