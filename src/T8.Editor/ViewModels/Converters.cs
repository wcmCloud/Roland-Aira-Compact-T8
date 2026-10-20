using Avalonia.Data.Converters;
using Avalonia.Media;
using T8.Editor.Services;

namespace T8.Editor.ViewModels;

public static class Converters
{
    /// <summary>Dims the note name on steps that don't play.</summary>
    public static readonly IValueConverter HasNoteOpacity =
        new FuncValueConverter<bool, double>(hasNote => hasNote ? 1.0 : 0.35);

    /// <summary>Green for a passed connection test, red for failed, normal while running.</summary>
    public static readonly IValueConverter TestResultBrush =
        new FuncValueConverter<bool?, IBrush>(passed => passed switch
        {
            true => Brushes.LightGreen,
            false => new SolidColorBrush(Color.Parse("#FF6B6B")),
            null => Brushes.White,
        });

    public static readonly IValueConverter IsPreviewMode = new PlayModeConverter(PlayMode.PreviewNotes);
    public static readonly IValueConverter IsSyncMode = new PlayModeConverter(PlayMode.SyncT8Sequencer);

    /// <summary>Binds a radio button to one PlayMode value.</summary>
    private sealed class PlayModeConverter(PlayMode mode) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is PlayMode current && current == mode;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is true ? mode : Avalonia.Data.BindingOperations.DoNothing;
    }
}
