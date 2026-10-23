using System.Runtime;
using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;
namespace T8.Audio;

public delegate void RenderAudio(Span<float> stereo);
public interface IAudioDevice : IDisposable
{
    bool IsRunning { get; }
    string? Error { get; }
    void Start(RenderAudio render);
}

/// <summary>miniaudio supplies CoreAudio, WASAPI and Linux backends; no microphone is opened.</summary>
public sealed unsafe class AudioDevice : IAudioDevice
{
    private MaDevice* _device;
    private MaContext* _context;
    private bool _initialized;
    private readonly string? _name;
    private RenderAudio? _render;
    private readonly DataCallback _callback;
    private string? _error;
    private GCLatencyMode _previousLatency;
    private bool _lowLatency;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DataCallback(MaDevice* device, void* output, void* input, uint frames);
    public AudioDevice(string? name = null) { _name = name; _callback = Fill; }
    public string? Error => _error;
    public bool IsRunning => _initialized && MiniAudio.DeviceIsStarted(_device) != 0 && _error is null;
    private static void Check(MaResult result) { if (result != MaResult.Success) throw new InvalidOperationException($"Audio device: {result}."); }

    // The generated structs match the Windows build (context 720, device 3312). The same
    // package's Linux and macOS libraries are larger — Linux x64 context is 1024 bytes and
    // macOS device is 3944 — so sizing from sizeof overflows the heap and glibc aborts.
    private const nuint DeviceBytes = 8192;

    private static void* Alloc(nuint bytes)
    {
        const nuint alignment = 16;
        var size = Math.Max(alignment, (bytes + alignment - 1) & ~(alignment - 1));
        var memory = NativeMemory.AlignedAlloc(size, alignment);
        NativeMemory.Clear(memory, size);
        return memory;
    }

    private static MaContext* AllocContext() =>
        (MaContext*)Alloc(Math.Max((nuint)sizeof(MaContext), (nuint)MiniAudio.ContextSizeof()));

    private static MaContext* OpenContext()
    {
        var context = AllocContext();
        try { Check(MiniAudio.ContextInit((MaBackend*)null, 0, (MaContextConfig*)null, context)); return context; }
        catch { NativeMemory.AlignedFree(context); throw; }
    }
    public static IReadOnlyList<string> ListOutputs()
    {
        var context = OpenContext();
        try
        {
            MaDeviceInfo* playback = null; uint count = 0;
            Check(MiniAudio.ContextGetDevices(context, &playback, &count, (MaDeviceInfo**)null, (uint*)null));
            var names = new List<string>();
            for (int i = 0; i < count; i++) names.Add(Marshal.PtrToStringUTF8((nint)(&playback[i].Name_0))!);
            return names;
        }
        finally { MiniAudio.ContextUninit(context); NativeMemory.AlignedFree(context); }
    }
    public void Start(RenderAudio render)
    {
        if (_initialized) throw new InvalidOperationException("Audio device already started.");
        _render = render;
        try
        {
            _context = OpenContext();
            var config = MiniAudio.DeviceConfigInit(MaDeviceType.Playback);
            config.Playback.Format = MaFormat.F32; config.Playback.Channels = 2;
            // 256 frames is about 5 ms. A garbage-collection pause then drops the callback and the loop clicks.
            config.SampleRate = 48000; config.PeriodSizeInFrames = 1024; config.Periods = 3;
            config.DataCallback = (void*)Marshal.GetFunctionPointerForDelegate(_callback);
            MaDeviceId chosen = default;
            if (_name is not null)
            {
                MaDeviceInfo* playback = null; uint count = 0;
                Check(MiniAudio.ContextGetDevices(_context, &playback, &count, (MaDeviceInfo**)null, (uint*)null));
                bool found = false;
                for (int i = 0; i < count; i++)
                    if (Marshal.PtrToStringUTF8((nint)(&playback[i].Name_0)) == _name) { chosen = playback[i].Id; found = true; break; }
                if (!found) throw new InvalidOperationException("Selected audio output is unavailable. Choose System default or another output.");
                config.Playback.PDeviceID = &chosen;
            }
            _device = (MaDevice*)Alloc(Math.Max((nuint)sizeof(MaDevice), DeviceBytes));
            Check(MiniAudio.DeviceInit(_context, &config, _device)); _initialized = true;
            Check(MiniAudio.DeviceStart(_device));
            _previousLatency = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            _lowLatency = true;
        }
        catch { Dispose(); throw; }
    }
    private void Fill(MaDevice* device, void* output, void* input, uint frames)
    {
        var buffer = new Span<float>(output, checked((int)frames * 2));
        try { if (_error is null) _render!(buffer); else buffer.Clear(); }
        catch { buffer.Clear(); _error = "Audio rendering stopped. Select an audio output and press Play to retry."; }
    }
    public void Dispose()
    {
        // Uninit waits for the callback before its storage and delegate are released.
        if (_initialized) { MiniAudio.DeviceUninit(_device); _initialized = false; }
        if (_device != null) { NativeMemory.AlignedFree(_device); _device = null; }
        if (_context != null) { MiniAudio.ContextUninit(_context); NativeMemory.AlignedFree(_context); _context = null; }
        _render = null;
        if (_lowLatency) { GCSettings.LatencyMode = _previousLatency; _lowLatency = false; }
        GC.KeepAlive(_callback);
    }
}
