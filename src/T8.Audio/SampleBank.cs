using System.Reflection;
namespace T8.Audio;

/// <summary>Preloaded mono PCM. All I/O and resampling happen before the audio callback starts.</summary>
public sealed class SampleBank
{
    private static readonly Dictionary<int, SampleBank> SharedBanks = new();
    private readonly Dictionary<string, float[]> _samples = new();
    private readonly Dictionary<string, float[][]> _kits = new();
    public int SampleRate { get; }

    /// <summary>Decoded once per sample rate. Every voice reads these arrays; playback never opens the WAVs again.</summary>
    public static SampleBank Shared(int sampleRate = 48000)
    {
        lock (SharedBanks)
        {
            if (!SharedBanks.TryGetValue(sampleRate, out var bank))
                SharedBanks[sampleRate] = bank = new SampleBank(sampleRate);
            return bank;
        }
    }

    public SampleBank(int sampleRate = 48000)
    {
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate = sampleRate;
        var assembly = typeof(SampleBank).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".WAV")))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            _samples[name.Split('.')[^2]] = ReadWave(stream, sampleRate);
        }
        foreach (var preset in SoundPreset.All) _ = Kit(preset);
    }
    public float[][] Kit(SoundPreset preset)
    {
        if (_kits.TryGetValue(preset.Id, out var kit)) return kit;
        kit = new[] { preset.Kick, preset.Snare, preset.Tom, "CP", "CH", preset.OpenHat }
            .Select(n => _samples.TryGetValue(n, out var data) ? data : throw new InvalidDataException($"Missing drum sample: {n}")).ToArray();
        return _kits[preset.Id] = kit;
    }

    public static float[] ReadWave(Stream stream, int outputRate)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.ASCII, true);
        if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a RIFF file.");
        r.ReadUInt32();
        if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV file.");
        ushort channels = 0, bits = 0, format = 0; int rate = 0; byte[]? pcm = null;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = new string(r.ReadChars(4)); var size = r.ReadUInt32();
            var end = stream.Position + size;
            if (end > stream.Length) throw new InvalidDataException("Truncated WAV.");
            if (id == "fmt " && size >= 16)
            {
                format = r.ReadUInt16(); channels = r.ReadUInt16(); rate = r.ReadInt32();
                r.ReadInt32(); r.ReadUInt16(); bits = r.ReadUInt16();
            }
            else if (id == "data") pcm = r.ReadBytes(checked((int)size));
            stream.Position = end + (size & 1);
        }
        if (format != 1 || bits != 16 || channels != 1 || rate <= 0 || pcm is null || pcm.Length < 2 || outputRate <= 0)
            throw new InvalidDataException("Expected mono 16-bit PCM WAV.");
        var source = new float[pcm.Length / 2]; float peak = 0;
        for (var i = 0; i < source.Length; i++) { source[i] = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2)) / 32768f; peak = Math.Max(peak, Math.Abs(source[i])); }
        if (peak == 0) throw new InvalidDataException("Silent drum sample.");
        var result = new float[(int)Math.Ceiling(source.Length * (double)outputRate / rate)];
        for (var i = 0; i < result.Length; i++)
        {
            double pos = i * (double)rate / outputRate; int a = (int)pos, b = Math.Min(a + 1, source.Length - 1);
            var value = source[a] + (source[b] - source[a]) * (pos - a);
            // Normalize with headroom and a short end fade.
            result[i] = (float)(value / peak * .7 * Math.Min(1, (result.Length - i) / (outputRate * .003)));
        }
        return result;
    }
}
