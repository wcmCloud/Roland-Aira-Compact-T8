using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

// Renders the app icon SVG to every size the platforms need:
//   png/app-icon-<size>.png  16-1024 (Linux uses 16-512; 256 is also the window icon)
//   app-icon.ico             16-256, PNG entries (Windows exe)
//   app-icon.icns            16-1024, PNG entries (macOS bundle)
// Usage: dotnet run --project tools/IconRenderer -- [assets/icon]
var folder = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("assets", "icon"));
var svgPath = Path.Combine(folder, "app-icon.svg");
var pngFolder = Path.Combine(folder, "png");
Directory.CreateDirectory(pngFolder);

using var svg = new SKSvg();
if (svg.Load(svgPath)?.CullRect is not { } bounds || bounds.Width <= 0)
{
    Console.Error.WriteLine($"Couldn't read {svgPath}");
    return 1;
}

var pngs = new Dictionary<int, byte[]>();
foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 })
{
    using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    using (var canvas = new SKCanvas(bitmap))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / bounds.Width, size / bounds.Height);
        canvas.DrawPicture(svg.Picture);
    }
    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    pngs[size] = data.ToArray();
    File.WriteAllBytes(Path.Combine(pngFolder, $"app-icon-{size}.png"), pngs[size]);
}

File.WriteAllBytes(Path.Combine(folder, "app-icon.ico"), Ico([16, 24, 32, 48, 64, 128, 256]));
File.WriteAllBytes(Path.Combine(folder, "app-icon.icns"), Icns(
[
    ("icp4", 16), ("icp5", 32), ("icp6", 64), ("ic07", 128), ("ic08", 256), ("ic09", 512),
    ("ic10", 1024), ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512),
]));
Console.WriteLine($"Icon files written to {folder}");
return 0;

// Windows .ico with PNG images (Windows Vista and later).
byte[] Ico(int[] sizes)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    foreach (var size in sizes)
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(pngs[size].Length);
        writer.Write(offset);
        offset += pngs[size].Length;
    }
    foreach (var size in sizes)
        writer.Write(pngs[size]);
    return stream.ToArray();
}

// macOS .icns with PNG images: "icns", total length, then (type, length, PNG) entries, big-endian.
byte[] Icns((string Type, int Size)[] entries)
{
    using var body = new MemoryStream();
    var header = new byte[8];
    foreach (var (type, size) in entries)
    {
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), 8 + pngs[size].Length);
        body.Write(header);
        body.Write(pngs[size]);
    }
    var result = new byte[8 + body.Length];
    System.Text.Encoding.ASCII.GetBytes("icns").CopyTo(result, 0);
    BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(4), result.Length);
    body.ToArray().CopyTo(result, 8);
    return result;
}
