namespace T8.Core.Patterns;

/// <summary>
/// Shared shape of a bank of variations: eight related patterns, the first close to the
/// original and the last using the full knobs.
/// </summary>
public static class PatternVariation
{
    public const int BankSize = 8;

    /// <summary>A seed for variation <paramref name="index"/> (0-based) that stays clear of <paramref name="seed"/>.</summary>
    public static int Seed(int seed, int index) => unchecked(seed + (index + 1) * 7919);

    /// <summary>
    /// How far variation <paramref name="index"/> strays, from about 0.3 for the first of
    /// <paramref name="count"/> to 1 for the last.
    /// </summary>
    public static double Amount(int index, int count)
    {
        if (count <= 1)
            return 1;
        index = Math.Clamp(index, 0, count - 1);
        return 0.3 + 0.7 * index / (count - 1);
    }
}
