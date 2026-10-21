using Avalonia.Data.Converters;
using Avalonia.Media;
using T8.Editor.Services;

namespace T8.Editor.ViewModels;

public static class Converters
{
    /// <summary>Fills are drawn a little dimmer than the entries on the song timeline.</summary>
    public static readonly IValueConverter FillOpacity =
        new FuncValueConverter<bool, double>(isFill => isFill ? 0.6 : 1.0);

    /// <summary>"#RRGGBB" to a brush.</summary>
    public static readonly IValueConverter HexBrush =
        new FuncValueConverter<string?, IBrush>(hex => new SolidColorBrush(ParseColor(hex)));

    /// <summary>Strength of a song colour for parts not playing (rows and the timeline's background).</summary>
    public const double TintOpacity = 0.28;

    /// <summary>Strength of a song colour for the playing part (its row, and what has played on the timeline).</summary>
    public const double PlayingOpacity = 0.7;

    /// <summary>"#RRGGBB" to a see-through tint of it, for backgrounds.</summary>
    public static readonly IValueConverter HexTint =
        new FuncValueConverter<string?, IBrush>(hex => new SolidColorBrush(ParseColor(hex), TintOpacity));

    /// <summary>"#RRGGBB" to the stronger shade used for the playing part.</summary>
    public static readonly IValueConverter HexPlaying =
        new FuncValueConverter<string?, IBrush>(hex => new SolidColorBrush(ParseColor(hex), PlayingOpacity));

    /// <summary>A song row's background: its colour as a tint, or the stronger shade while it plays (as on the timeline).</summary>
    public static readonly IMultiValueConverter SongRowBrush =
        new FuncMultiValueConverter<object?, IBrush>(values =>
        {
            var list = values.ToList();
            var hex = list.ElementAtOrDefault(0) as string;
            var playing = list.ElementAtOrDefault(1) is true;
            return new SolidColorBrush(ParseColor(hex), playing ? PlayingOpacity : TintOpacity);
        });

    private static Color ParseColor(string? hex) => Color.TryParse(hex, out var color) ? color : Color.Parse("#FF7A1A");

    /// <summary>A channel number, or "OFF" for a part that is switched off.</summary>
    public static readonly IValueConverter ChannelName =
        new FuncValueConverter<int, string>(T8.Midi.T8MidiMap.ChannelName);

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
