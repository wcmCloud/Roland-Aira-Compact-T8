using T8.Core.Prm;

namespace T8.Core.Tests;

public class PrmDocumentTests
{
    [Fact]
    public void Parses_keys_values_in_order()
    {
        var doc = PrmDocument.Parse("LENGTH\t= 16\nSTEP 1\t= STATE=1 NOTE=36\n");

        Assert.Equal(["LENGTH", "STEP 1"], doc.Entries.Select(e => e.Key));
        Assert.Equal("16", doc["LENGTH"]);
        Assert.Equal("STATE=1 NOTE=36", doc["STEP 1"]);
    }

    [Fact]
    public void Accepts_windows_line_endings_and_blank_lines()
    {
        var doc = PrmDocument.Parse("LENGTH\t= 8\r\n\r\nTRIPLET\t= 0\r\n");
        Assert.Equal("8", doc["LENGTH"]);
        Assert.Equal("0", doc["TRIPLET"]);
    }

    [Fact]
    public void Writes_t8_layout_with_tab_and_lf()
    {
        var doc = new PrmDocument();
        doc.Add("LENGTH", "16");
        doc.Add("FLAM", "36");
        Assert.Equal("LENGTH\t= 16\nFLAM\t= 36\n", doc.ToString());
    }

    [Fact]
    public void Missing_key_throws() =>
        Assert.Throws<KeyNotFoundException>(() => PrmDocument.Parse("LENGTH\t= 16\n")["SCALE"]);

    [Fact]
    public void Line_without_equals_throws() =>
        Assert.Throws<FormatException>(() => PrmDocument.Parse("garbage\n"));

    [Fact]
    public void Parses_fields()
    {
        var fields = PrmDocument.ParseFields("STATE=1 NOTE=46  ACCENT=0");
        Assert.Equal("1", fields["STATE"]);
        Assert.Equal("46", fields["NOTE"]);
        Assert.Equal("0", fields["ACCENT"]);
    }

    [Fact]
    public void Field_without_equals_throws() =>
        Assert.Throws<FormatException>(() => PrmDocument.ParseFields("STATE=1 NOTE"));

    [Fact]
    public void ParseInt_rejects_non_numbers() =>
        Assert.Throws<FormatException>(() => PrmDocument.ParseInt("x"));

    [Fact]
    public void Save_and_load_round_trip()
    {
        var path = Path.GetTempFileName();
        try
        {
            var doc = new PrmDocument();
            doc.Add("LENGTH", "12");
            doc.Save(path);
            Assert.Equal("LENGTH\t= 12\n", File.ReadAllText(path));
            Assert.Equal("12", PrmDocument.Load(path)["LENGTH"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
