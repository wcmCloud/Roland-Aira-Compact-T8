using Avalonia.Data.Converters;

namespace T8.Editor.ViewModels;

public static class Converters
{
    /// <summary>Dims the note name on steps that don't play.</summary>
    public static readonly IValueConverter HasNoteOpacity =
        new FuncValueConverter<bool, double>(hasNote => hasNote ? 1.0 : 0.35);
}
