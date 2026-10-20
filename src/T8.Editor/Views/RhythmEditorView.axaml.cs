using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

public partial class RhythmEditorView : UserControl
{
    public RhythmEditorView()
    {
        InitializeComponent();
        // Ctrl/Cmd/Shift-click picks active cells for the multi-selection instead of switching them.
        TrackRows.AddHandler(PointerPressedEvent, OnCellPointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>Ctrl (Windows/Linux), Cmd (macOS) or Shift held: a click picks instead of toggles.</summary>
    public static bool IsPickGesture(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta) || modifiers.HasFlag(KeyModifiers.Shift);

    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPickGesture(e.KeyModifiers) || DataContext is not RhythmEditorViewModel editor)
            return;
        var cell = (e.Source as Avalonia.Visual)?.GetSelfAndVisualAncestors().OfType<ToggleButton>()
            .FirstOrDefault(b => b.Classes.Contains("cell"))?.DataContext as RhythmCellViewModel;
        if (cell is null)
            return;
        editor.ToggleMultiSelect(cell);
        e.Handled = true;
    }
}
