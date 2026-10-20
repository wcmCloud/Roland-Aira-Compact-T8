using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace T8.Editor.Views;

/// <summary>
/// A rotary knob with its value and label underneath. Drag up or down (or scroll) to turn it;
/// double-click to return to <see cref="DefaultValue"/>.
/// </summary>
public sealed class Knob : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceValue);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Step), 1);

    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Knob, double?>(nameof(DefaultValue));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Knob, string?>(nameof(Label));

    /// <summary>Format for the value text, e.g. "{0}%". Defaults to the plain number.</summary>
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Format), "{0}");

    public static readonly StyledProperty<IBrush> AccentProperty =
        AvaloniaProperty.Register<Knob, IBrush>(nameof(Accent), new SolidColorBrush(Color.Parse("#FF7A1A")));

    private const double Dial = 44;
    private const double Width0 = 70;
    private const double StartAngle = 135;
    private const double Sweep = 270;
    /// <summary>Pixels of drag for the full range.</summary>
    private const double DragRange = 160;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#2C2C32"));
    private static readonly IBrush BodyBrush = new SolidColorBrush(Color.Parse("#1E1E22"));
    private static readonly IPen BodyPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A42")), 1);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#9A9AA2"));

    private Point? _dragStart;
    private double _dragValue;

    static Knob()
    {
        AffectsRender<Knob>(ValueProperty, MinimumProperty, MaximumProperty, LabelProperty, FormatProperty, AccentProperty);
        FocusableProperty.OverrideDefaultValue<Knob>(true);
    }

    public Knob() => Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public IBrush Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Value as a fraction 0–1 of the range.</summary>
    public double Fraction => Maximum > Minimum ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1) : 0;

    /// <summary>Returns to <see cref="DefaultValue"/>, as a double-click does.</summary>
    public void Reset()
    {
        if (DefaultValue is { } value)
            Value = value;
    }

    /// <summary>Turns the knob by a number of steps, as the mouse wheel does.</summary>
    public void Nudge(int steps) => Value = Snap(Value + steps * Step);

    private static double CoerceValue(AvaloniaObject sender, double value)
    {
        var knob = (Knob)sender;
        return Math.Clamp(value, knob.Minimum, Math.Max(knob.Minimum, knob.Maximum));
    }

    private double Snap(double value)
    {
        var step = Step > 0 ? Step : 1;
        return Math.Clamp(Math.Round((value - Minimum) / step) * step + Minimum, Minimum, Maximum);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Width0, Dial + (Label is null ? 18 : 32));

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Dial / 2);
        var radius = Dial / 2 - 3;

        DrawArc(context, center, radius, StartAngle, Sweep, TrackBrush);
        if (Fraction > 0)
            DrawArc(context, center, radius, StartAngle, Sweep * Fraction, Accent);

        context.DrawEllipse(BodyBrush, BodyPen, center, radius - 6, radius - 6);
        var angle = (StartAngle + Sweep * Fraction) * Math.PI / 180;
        var tip = new Point(center.X + Math.Cos(angle) * (radius - 9), center.Y + Math.Sin(angle) * (radius - 9));
        var inner = new Point(center.X + Math.Cos(angle) * 4, center.Y + Math.Sin(angle) * 4);
        context.DrawLine(new Pen(Brushes.White, 2, lineCap: PenLineCap.Round), inner, tip);

        var valueText = Text(string.Format(CultureInfo.InvariantCulture, Format, Math.Round(Value, 2)), 11, Brushes.White, FontWeight.SemiBold);
        context.DrawText(valueText, new Point((Bounds.Width - valueText.Width) / 2, Dial + 1));
        if (Label is { } label)
        {
            var labelText = Text(label, 10.5, LabelBrush, FontWeight.Normal);
            context.DrawText(labelText, new Point((Bounds.Width - labelText.Width) / 2, Dial + 16));
        }
    }

    private static FormattedText Text(string text, double size, IBrush brush, FontWeight weight) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, weight: weight), size, brush);

    private static void DrawArc(DrawingContext context, Point center, double radius, double start, double sweep, IBrush brush)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointAt(center, radius, start), false);
            ctx.ArcTo(PointAt(center, radius, start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, 4, lineCap: PenLineCap.Round), geometry);
    }

    private static Point PointAt(Point center, double radius, double degrees)
    {
        var a = degrees * Math.PI / 180;
        return new Point(center.X + Math.Cos(a) * radius, center.Y + Math.Sin(a) * radius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.ClickCount == 2 && DefaultValue is not null)
        {
            Reset();
            e.Handled = true;
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragValue = Value;
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragStart is not { } start)
            return;
        var dy = start.Y - e.GetPosition(this).Y;
        // Shift turns it finely.
        var scale = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.2 : 1;
        Value = Snap(_dragValue + dy / DragRange * (Maximum - Minimum) * scale);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragStart = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Nudge(e.Delta.Y > 0 ? 1 : e.Delta.Y < 0 ? -1 : 0);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Up or Key.Right:
                Nudge(1);
                e.Handled = true;
                break;
            case Key.Down or Key.Left:
                Nudge(-1);
                e.Handled = true;
                break;
        }
    }
}
