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
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DataCallback(MaDevice* device, void* output, void* input, uint frames);
    public AudioDevice(string? name = null) { _name = name; _callback = Fill; }
    public string? Error => _error;
    public bool IsRunning => _initialized && MiniAudio.DeviceIsStarted(_device) != 0 && _error is null;
    private static void Check(MaResult result) { if (result != MaResult.Success) throw new InvalidOperationException($"Audio device: {result}."); }
    private static MaContext* OpenContext()
    {
        var context = (MaContext*)NativeMemory.AllocZeroed((nuint)sizeof(MaContext));
        try { Check(MiniAudio.ContextInit((MaBackend*)null, 0, (MaContextConfig*)null, context)); return context; }
        catch { NativeMemory.Free(context); throw; }
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
        finally { MiniAudio.ContextUninit(context); NativeMemory.Free(context); }
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
            config.SampleRate = 48000; config.PeriodSizeInFrames = 256; config.Periods = 2;
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
            _device = (MaDevice*)NativeMemory.AllocZeroed((nuint)sizeof(MaDevice));
            Check(MiniAudio.DeviceInit(_context, &config, _device)); _initialized = true;
            Check(MiniAudio.DeviceStart(_device));
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
        if (_device != null) { NativeMemory.Free(_device); _device = null; }
        if (_context != null) { MiniAudio.ContextUninit(_context); NativeMemory.Free(_context); _context = null; }
        _render = null;
        GC.KeepAlive(_callback);
    }
}
