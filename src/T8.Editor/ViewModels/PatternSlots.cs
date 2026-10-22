namespace T8.Editor.ViewModels;

/// <summary>Formats bank and pattern numbers for status text, collapsing runs such as 13–16.</summary>
internal static class PatternSlots
{
    public static string Describe(IReadOnlyList<(int Bank, int Pattern)> slots)
    {
        if (slots.Count == 0)
            return "no patterns";

        var parts = new List<string>();
        var index = 0;
        while (index < slots.Count)
        {
            var bank = slots[index].Bank;
            var numbers = new List<int>();
            while (index < slots.Count && slots[index].Bank == bank)
            {
                numbers.Add(slots[index].Pattern);
                index++;
            }
            parts.Add($"bank {bank} {FormatPatterns(numbers)}");
        }
        return JoinAnd(parts);
    }

    private static string FormatPatterns(IReadOnlyList<int> numbers)
    {
        var ranges = new List<string>();
        var i = 0;
        while (i < numbers.Count)
        {
            var start = numbers[i];
            var end = start;
            while (i + 1 < numbers.Count && numbers[i + 1] == end + 1)
            {
                i++;
                end = numbers[i];
            }
            ranges.Add(start == end ? $"{start}" : $"{start}–{end}");
            i++;
        }
        var noun = numbers.Count == 1 ? "pattern" : "patterns";
        return $"{noun} {JoinAnd(ranges)}";
    }

    private static string JoinAnd(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => "",
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
    };
}
