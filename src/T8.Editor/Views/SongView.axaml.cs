using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

public partial class SongView : UserControl
{
    public SongView()
    {
        InitializeComponent();
        SongTimeline.AddHandler(PointerPressedEvent, OnTimelinePressed, RoutingStrategies.Tunnel);
    }

    /// <summary>A click on a timeline block plays that part next.</summary>
    private void OnTimelinePressed(object? sender, PointerPressedEventArgs e)
    {
        var block = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>()
            .Select(c => c.DataContext).OfType<TimelineBlock>().FirstOrDefault();
        if (block is null || DataContext is not SongViewModel song)
            return;
        song.QueueEntry(block.Entry);
        e.Handled = true;
    }
}
