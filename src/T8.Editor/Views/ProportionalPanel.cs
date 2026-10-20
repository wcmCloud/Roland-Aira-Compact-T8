using Avalonia;
using Avalonia.Controls;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

/// <summary>Lays out song timeline blocks side by side, each as wide as its share of the song.</summary>
public sealed class ProportionalPanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ProportionalPanel, double>(nameof(Gap), 2);

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    private static double Share(Control child) => child.DataContext is TimelineBlock block ? block.Share : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var usable = Math.Max(0, finalSize.Width - Gap * Math.Max(0, Children.Count - 1));
        var x = 0.0;
        foreach (var child in Children)
        {
            var width = usable * Share(child);
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Gap;
        }
        return finalSize;
    }
}
