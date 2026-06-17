using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sonar.AutoSwitch.Services;

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
}

[ComImport]
[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    void GetPeak(out float pfPeak);
}

public sealed class AudioMeterService
{
    private static readonly Lazy<AudioMeterService> _lazy = new(() => new AudioMeterService());
    public static AudioMeterService Instance => _lazy.Value;

    // One meter per Windows audio role: eConsole=0, eMultimedia=1, eCommunications=2.
    // Sonar routes different app categories to different virtual endpoints, so we
    // poll all three and take the max — whichever role has audio playing wins.
    private readonly IAudioMeterInformation?[] _meters = new IAudioMeterInformation?[3];
    private readonly Timer _timer;

    public event EventHandler<float>? PeakChanged;

    private AudioMeterService()
    {
        TryInitMeters();
        _timer = new Timer(_ => Poll(), null, 0, 33);
    }

    private void TryInitMeters()
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumeratorType is null) return;
            var enumeratorObj = Activator.CreateInstance(enumeratorType);
            if (enumeratorObj is not IMMDeviceEnumerator enumerator) return;

            var meterIid = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
            for (var role = 0; role < 3; role++)
            {
                try
                {
                    enumerator.GetDefaultAudioEndpoint(0, role, out var device);
                    device.Activate(ref meterIid, 7, IntPtr.Zero, out var obj);
                    _meters[role] = obj as IAudioMeterInformation;
                }
                catch { }
            }
        }
        catch { }
    }

    private void Poll()
    {
        var peak = 0f;
        for (var i = 0; i < _meters.Length; i++)
        {
            if (_meters[i] is null) continue;
            try
            {
                _meters[i]!.GetPeak(out var p);
                if (p > peak) peak = p;
            }
            catch { _meters[i] = null; }
        }
        PeakChanged?.Invoke(this, peak);
    }
}
