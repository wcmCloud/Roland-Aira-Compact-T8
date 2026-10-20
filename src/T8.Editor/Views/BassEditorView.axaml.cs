using Avalonia.Controls;
using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

public partial class BassEditorView : UserControl
{
    public BassEditorView() => InitializeComponent();

    private void OnStepContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: BassStepViewModel step } control)
            return;
        step.SelectCommand.Execute(null);
        BuildStepMenu(step).Open(control);
        e.Handled = true;
    }

    /// <summary>Right-click menu for a step: every note in the T-8's range by octave, the step state, accent and slide.</summary>
    internal static ContextMenu BuildStepMenu(BassStepViewModel step)
    {
        var items = new List<Control>();
        foreach (var (octave, notes) in BassStepViewModel.NoteGroups)
        {
            var group = new MenuItem { Header = octave };
            if (step.HasNote && notes.Contains(step.Note))
                group.FontWeight = Avalonia.Media.FontWeight.Bold;
            group.ItemsSource = notes.Select(note => new MenuItem
            {
                Header = BassStepViewModel.FormatNote(note),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = step.HasNote && step.Note == note,
                Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => step.SetNote(note)),
            }).ToList();
            items.Add(group);
        }

        items.Add(new Separator());
        foreach (var (state, label) in new[] { (BassStepState.Off, "Off"), (BassStepState.Note, "Note"), (BassStepState.Tie, "Tie") })
        {
            items.Add(new MenuItem
            {
                Header = label,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = step.State == state,
                Command = step.SetStateCommand,
                CommandParameter = state,
            });
        }

        items.Add(new Separator());
        items.Add(new MenuItem
        {
            Header = "Accent",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = step.Accent,
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => step.Accent = !step.Accent),
        });
        items.Add(new MenuItem
        {
            Header = "Slide",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = step.Slide,
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => step.Slide = !step.Slide),
        });

        return new ContextMenu { ItemsSource = items };
    }
}
