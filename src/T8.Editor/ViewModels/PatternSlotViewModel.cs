using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace T8.Editor.ViewModels;

/// <summary>One cell of the bank × pattern grid.</summary>
public sealed partial class PatternSlotViewModel(int bank, int pattern, Action<PatternSlotViewModel> select) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>No bass notes and no drum hits.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>The generator's fill pattern in the selected bank.</summary>
    [ObservableProperty]
    private bool _isFill;

    public int Bank { get; } = bank;
    public int Pattern { get; } = pattern;

    public string ToolTip => $"Bank {Bank} · Pattern {Pattern}";

    [RelayCommand]
    private void Select() => select(this);
}

/// <summary>A bank's row in the pattern grid.</summary>
public sealed record PatternBankRow(int Bank, IReadOnlyList<PatternSlotViewModel> Slots)
{
    public string Label => $"BANK {Bank}";
}
