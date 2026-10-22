using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace T8.Editor.Views;

/// <summary>
/// A clickable piano keyboard. Clicking a key runs <see cref="Command"/> with the MIDI note number;
/// <see cref="HighlightedNote"/> is drawn in the T-8's orange.
/// </summary>
public sealed class PianoKeyboard : Control
{
    public static readonly StyledProperty<int> LowestNoteProperty =
        AvaloniaProperty.Register<PianoKeyboard, int>(nameof(LowestNote), 12);

    public static readonly StyledProperty<int> HighestNoteProperty =
        AvaloniaProperty.Register<PianoKeyboard, int>(nameof(HighestNote), 96);

    public static readonly StyledProperty<int?> HighlightedNoteProperty =
        AvaloniaProperty.Register<PianoKeyboard, int?>(nameof(HighlightedNote));

    public static readonly StyledProperty<int?> PlayingNoteProperty =
        AvaloniaProperty.Register<PianoKeyboard, int?>(nameof(PlayingNote));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PianoKeyboard, ICommand?>(nameof(Command));

    public const double WhiteWidth = 28;
    private const double WhiteHeight = 110;
    private const double BlackWidth = 17;
    private const double BlackHeight = 66;

    private static readonly IBrush WhiteBrush = new SolidColorBrush(Color.Parse("#E8E8E8"));
    private static readonly IBrush WhiteHoverBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush BlackBrush = new SolidColorBrush(Color.Parse("#1A1A1C"));
    private static readonly IBrush BlackHoverBrush = new SolidColorBrush(Color.Parse("#48484E"));
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.Parse("#FF7A1A"));
    private static readonly IBrush PlayingBrush = new SolidColorBrush(Color.Parse("#FFD23F"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#606066"));
    private static readonly IPen KeyPen = new Pen(new SolidColorBrush(Color.Parse("#0E0E10")), 1);

    private int? _hoverNote;

    static PianoKeyboard()
    {
        AffectsMeasure<PianoKeyboard>(LowestNoteProperty, HighestNoteProperty);
        AffectsRender<PianoKeyboard>(HighlightedNoteProperty, PlayingNoteProperty);
    }

    public PianoKeyboard() => Cursor = new Cursor(StandardCursorType.Hand);

    public int LowestNote
    {
        get => GetValue(LowestNoteProperty);
        set => SetValue(LowestNoteProperty, value);
    }

    public int HighestNote
    {
        get => GetValue(HighestNoteProperty);
        set => SetValue(HighestNoteProperty, value);
    }

    public int? HighlightedNote
    {
        get => GetValue(HighlightedNoteProperty);
        set => SetValue(HighlightedNoteProperty, value);
    }

    /// <summary>The note being played, drawn in yellow over the highlight.</summary>
    public int? PlayingNote
    {
        get => GetValue(PlayingNoteProperty);
        set => SetValue(PlayingNoteProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static bool IsBlack(int note) => (note % 12) is 1 or 3 or 6 or 8 or 10;

    /// <summary>The key under a point, black keys first, or null outside the keys.</summary>
    public int? NoteAt(Point point)
    {
        if (point.Y < 0 || point.Y > WhiteHeight)
            return null;
        if (point.Y <= BlackHeight)
        {
            foreach (var (note, rect) in BlackKeys())
            {
                if (rect.Contains(point))
                    return note;
            }
        }
        foreach (var (note, rect) in WhiteKeys())
        {
            if (rect.Contains(point))
                return note;
        }
        return null;
    }

    /// <summary>Presses a key as a click would.</summary>
    public void Press(int note)
    {
        if (Command?.CanExecute(note) == true)
            Command.Execute(note);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(WhiteKeys().Count() * WhiteWidth + 1, WhiteHeight + 1);

    public override void Render(DrawingContext context)
    {
        foreach (var (note, rect) in WhiteKeys())
        {
            var brush = KeyBrush(note, WhiteHoverBrush, WhiteBrush);
            context.DrawRectangle(brush, KeyPen, rect, 2, 2);
            if (note % 12 == 0)
            {
                var label = new FormattedText($"C{note / 12 - 1}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 10, note == HighlightedNote ? Brushes.White : LabelBrush);
                context.DrawText(label, new Point(rect.X + (rect.Width - label.Width) / 2, rect.Bottom - label.Height - 4));
            }
        }
        foreach (var (note, rect) in BlackKeys())
        {
            var brush = KeyBrush(note, BlackHoverBrush, BlackBrush);
            context.DrawRectangle(brush, KeyPen, rect, 0, 2);
        }
    }

    private IBrush KeyBrush(int note, IBrush hover, IBrush normal) =>
        note == PlayingNote ? PlayingBrush
        : note == HighlightedNote ? HighlightBrush
        : note == _hoverNote ? hover
        : normal;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || NoteAt(e.GetPosition(this)) is not { } note)
            return;
        Press(note);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHover(NoteAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    private void SetHover(int? note)
    {
        if (_hoverNote == note)
            return;
        _hoverNote = note;
        ToolTip.SetTip(this, note is { } n ? T8.Editor.ViewModels.BassStepViewModel.FormatNote(n) : null);
        InvalidateVisual();
    }

    private IEnumerable<(int Note, Rect Rect)> WhiteKeys()
    {
        var x = 0.0;
        for (var note = LowestNote; note <= HighestNote; note++)
        {
            if (IsBlack(note))
                continue;
            yield return (note, new Rect(x, 0, WhiteWidth, WhiteHeight));
            x += WhiteWidth;
        }
    }

    private IEnumerable<(int Note, Rect Rect)> BlackKeys()
    {
        // A black key sits on the line between the white key before it and the one after.
        foreach (var (note, rect) in WhiteKeys())
        {
            if (note + 1 <= HighestNote && IsBlack(note + 1))
                yield return (note + 1, new Rect(rect.Right - BlackWidth / 2, 0, BlackWidth, BlackHeight));
        }
    }
}
