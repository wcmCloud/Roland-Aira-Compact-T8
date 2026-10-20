using T8.Core.Patterns;
using T8.Core.Songs;

namespace T8.Core.Tests;

public class SongBankWriterTests
{
    /// <summary>A library where every pattern is marked: rhythm length = 10 + pattern number, bass length = bank number.</summary>
    private static PatternLibrary Marked()
    {
        var library = PatternLibrary.CreateBlank();
        for (var b = 0; b < PatternLibrary.BankCount; b++)
        {
            for (var p = 0; p < PatternLibrary.PatternsPerBank; p++)
            {
                library.Rhythm[b, p].Length = 11 + p; // 10 + the 1-based pattern number
                library.Bass[b, p].Length = b + 1;
                library.Rhythm[b, p].Steps[0][RhythmTrack.BD].On = true;
            }
        }
        return library;
    }

    private static (int Bank, int RhythmLength) Mark(PatternLibrary library, int bank, int pattern) =>
        (library.Bass[bank - 1, pattern - 1].Length, library.Rhythm[bank - 1, pattern - 1].Length);

    [Fact]
    public void Copies_entries_in_order_and_leaves_the_rest_of_the_bank_empty()
    {
        var library = Marked();
        var song = new Song { Entries = [new SongEntry { Bank = 1, Pattern = 5 }, new SongEntry { Bank = 3, Pattern = 2, Repeats = 4 }] };

        SongBankWriter.CopyToBank(song, library, bank: 4);

        Assert.Equal((1, 15), Mark(library, 4, 1)); // bank 1 pattern 5
        Assert.Equal((3, 12), Mark(library, 4, 2)); // bank 3 pattern 2
        for (var p = 3; p <= 16; p++)
            Assert.DoesNotContain(library.Rhythm[3, p - 1].Steps, s => s.Cells.Any(c => c.On));
        Assert.Equal([(4, 1), (4, 2)], song.Entries.Select(e => (e.Bank, e.Pattern)));
        Assert.Equal(4, song.Entries[1].Repeats); // repeats stay in the song
        Assert.Equal((1, 15), Mark(library, 1, 5)); // the sources are untouched
    }

    [Fact]
    public void Uses_copies_taken_before_the_bank_is_overwritten()
    {
        var library = Marked();
        // Bank 2's own patterns, in an order that overwrites them as it goes.
        var song = new Song
        {
            Entries =
            [
                new SongEntry { Bank = 2, Pattern = 3 },
                new SongEntry { Bank = 2, Pattern = 1 },
                new SongEntry { Bank = 2, Pattern = 2 },
                new SongEntry { Bank = 2, Pattern = 1 },
            ],
        };

        SongBankWriter.CopyToBank(song, library, bank: 2);

        Assert.Equal([13, 11, 12, 11], Enumerable.Range(1, 4).Select(p => Mark(library, 2, p).RhythmLength));
        Assert.NotSame(library.Rhythm[1, 1], library.Rhythm[1, 3]); // each slot gets its own copy
    }

    [Fact]
    public void Fills_go_after_the_entries_once_each_and_the_song_follows()
    {
        var library = Marked();
        var song = new Song
        {
            Entries =
            [
                new SongEntry { Bank = 1, Pattern = 1, Fill = new SongFill(1, 16) },
                new SongEntry { Bank = 1, Pattern = 2, Fill = new SongFill(1, 16) },
                new SongEntry { Bank = 1, Pattern = 3, Fill = new SongFill(1, 15) },
            ],
        };
        Assert.Equal(5, SongBankWriter.SlotsNeeded(song));

        SongBankWriter.CopyToBank(song, library, bank: 1);

        Assert.Equal(26, Mark(library, 1, 4).RhythmLength); // fill 1-16
        Assert.Equal(25, Mark(library, 1, 5).RhythmLength); // fill 1-15
        Assert.Equal([new SongFill(1, 4), new SongFill(1, 4), new SongFill(1, 5)], song.Entries.Select(e => e.Fill));
    }

    [Fact]
    public void Refuses_an_empty_song_or_one_that_needs_more_than_16_patterns()
    {
        Assert.Contains("no patterns", SongBankWriter.Problem(new Song()));

        var big = new Song { Entries = Enumerable.Range(1, 17).Select(_ => new SongEntry()).ToList() };
        Assert.Contains("17 entries", SongBankWriter.Problem(big));
        Assert.Throws<InvalidOperationException>(() => SongBankWriter.CopyToBank(big, PatternLibrary.CreateBlank(), 1));

        var withFill = new Song { Entries = Enumerable.Range(1, 16).Select(_ => new SongEntry { Fill = new SongFill(1, 16) }).ToList() };
        Assert.Contains("17 (16 entries and 1 fills)", SongBankWriter.Problem(withFill));

        var exactly16 = new Song { Entries = Enumerable.Range(1, 16).Select(_ => new SongEntry()).ToList() };
        Assert.Null(SongBankWriter.Problem(exactly16));
    }
}
