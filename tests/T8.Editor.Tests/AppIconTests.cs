using System.Buffers.Binary;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

namespace T8.Editor.Tests;

/// <summary>The rendered icon files in assets/icon (see scripts/render-icon.sh) exist and load.</summary>
public class AppIconTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G'];

    private static string IconFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "T8Editor.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "assets", "icon");
    }

    [Fact]
    public void The_svg_is_there_and_has_no_wordmark()
    {
        var svg = File.ReadAllText(Path.Combine(IconFolder(), "app-icon.svg"));
        Assert.Contains("<svg", svg);
        Assert.DoesNotContain("<text", svg);
    }

    [AvaloniaTheory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void Each_png_loads_at_its_size(int size)
    {
        using var bitmap = new Bitmap(Path.Combine(IconFolder(), "png", $"app-icon-{size}.png"));
        Assert.Equal(size, bitmap.PixelSize.Width);
        Assert.Equal(size, bitmap.PixelSize.Height);
    }

    [AvaloniaFact]
    public void The_ico_holds_16_to_256_and_each_image_loads()
    {
        var ico = File.ReadAllBytes(Path.Combine(IconFolder(), "app-icon.ico"));
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        var count = BitConverter.ToUInt16(ico, 4);
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + 16 * i;
            var size = ico[entry] == 0 ? 256 : ico[entry];
            var length = BitConverter.ToInt32(ico, entry + 8);
            var offset = BitConverter.ToInt32(ico, entry + 12);
            using var bitmap = new Bitmap(new MemoryStream(ico, offset, length));
            Assert.Equal(size, bitmap.PixelSize.Width);
            sizes.Add(size);
        }
        Assert.Equal([16, 24, 32, 48, 64, 128, 256], sizes);
    }

    [AvaloniaFact]
    public void The_icns_holds_16_to_1024_and_each_image_loads()
    {
        var icns = File.ReadAllBytes(Path.Combine(IconFolder(), "app-icon.icns"));
        Assert.Equal("icns", System.Text.Encoding.ASCII.GetString(icns, 0, 4));
        Assert.Equal(icns.Length, BinaryPrimitives.ReadInt32BigEndian(icns.AsSpan(4)));

        var widths = new HashSet<int>();
        for (var at = 8; at < icns.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(icns.AsSpan(at + 4));
            Assert.True(icns.AsSpan(at + 8, 4).SequenceEqual(PngSignature));
            using var bitmap = new Bitmap(new MemoryStream(icns, at + 8, length - 8));
            widths.Add(bitmap.PixelSize.Width);
            at += length;
        }
        Assert.Superset(new HashSet<int> { 16, 32, 64, 128, 256, 512, 1024 }, widths);
    }
}
