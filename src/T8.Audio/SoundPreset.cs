namespace T8.Audio;

public enum BassWave { Saw, Square }
public sealed record BassSound(BassWave Wave = BassWave.Saw, double Cutoff = 1100, double Resonance = .7, double Decay = .25, double Slide = .075);
public sealed record SoundPreset(string Id, string Name, string Kick, string Snare, string Tom, string OpenHat,
    BassSound Bass, double Tone = 1, double Drive = 1, double DrumDecay = 1)
{
    public override string ToString() => Name;
    public static IReadOnlyList<SoundPreset> All { get; } = Array.AsReadOnly(new[]
    {
        new SoundPreset("classic", "Classic Analog", "BD5050", "SD5050", "LT50", "OH50", new()),
        new SoundPreset("deep", "Deep Electro", "BD0010", "SD0025", "LT00", "OH10", new(BassWave.Square, 500, .6, .45, .12), .7),
        new SoundPreset("tight", "Tight Machine", "BD7500", "SD7500", "LT75", "OH00", new(BassWave.Square, 1600, .7, .12, .035), 1, 1, .45),
        new SoundPreset("acid", "Bright Acid", "BD7550", "SD7575", "LT25", "OH75", new(BassWave.Saw, 2200, 2, .25, .14), 1, 1.2),
        new SoundPreset("warm", "Warm Tape", "BD2525", "SD2525", "LT50", "OH25", new(BassWave.Square, 650, .6, .3, .09), .4, 1.5, .8),
        new SoundPreset("gritty", "Gritty Club", "BD1010", "SD1010", "LT10", "OH50", new(BassWave.Saw, 2600, 1.1, .2, .06), .9, 2, .7),
    });
    public static SoundPreset Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

public sealed class OfflineSettings
{
    public string PresetId { get; set; } = "classic";
    public double Volume { get; set; } = .45;
    public string? DeviceName { get; set; }
    public BassSound? Bass { get; set; }
    public OfflineSettings Clone() => (OfflineSettings)MemberwiseClone();
    public void Validate()
    {
        PresetId = SoundPreset.Find(PresetId).Id;
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 1) : .45;
        if (Bass is { } b) Bass = Sanitize(b);
    }
    public static BassSound Sanitize(BassSound b) => new(Enum.IsDefined(b.Wave) ? b.Wave : BassWave.Saw,
        Clamp(b.Cutoff, 80, 10000, 1100), Clamp(b.Resonance, .5, 4, .7), Clamp(b.Decay, .04, 2, .25), Clamp(b.Slide, .005, .5, .075));
    private static double Clamp(double v, double min, double max, double fallback) => double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
}
