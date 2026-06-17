using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sonar.AutoSwitch.Services;

// COM interfaces for WASAPI peak meter — no NuGet packages required.

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    // Slot 0: EnumAudioEndpoints (not used, but must occupy slot)
    void EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);

    // Slot 1: GetDefaultAudioEndpoint
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    // Slot 0: Activate
    void Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
}

[ComImport]
[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    // Slot 0: GetPeak
    void GetPeak(out float pfPeak);
}

public sealed class AudioMeterService
{
    private static readonly Lazy<AudioMeterService> _lazy = new(() => new AudioMeterService());
    public static AudioMeterService Instance => _lazy.Value;

    private IAudioMeterInformation? _meter;
    private readonly Timer _timer;

    public event EventHandler<float>? PeakChanged;

    private AudioMeterService()
    {
        TryInitMeter();
        // Poll at ~30 fps (33ms interval)
        _timer = new Timer(_ => Poll(), null, 0, 33);
    }

    private void TryInitMeter()
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumeratorType is null) return;
            var enumeratorObj = Activator.CreateInstance(enumeratorType);
            if (enumeratorObj is not IMMDeviceEnumerator enumerator) return;

            // dataFlow=0 (eRender), role=1 (eMultimedia)
            enumerator.GetDefaultAudioEndpoint(0, 1, out var device);

            var meterIid = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
            // dwClsCtx=7 (CLSCTX_ALL)
            device.Activate(ref meterIid, 7, IntPtr.Zero, out var meterObj);

            _meter = meterObj as IAudioMeterInformation;
        }
        catch
        {
            _meter = null;
        }
    }

    private void Poll()
    {
        if (_meter is null)
        {
            PeakChanged?.Invoke(this, 0f);
            return;
        }

        try
        {
            _meter.GetPeak(out var peak);
            PeakChanged?.Invoke(this, peak);
        }
        catch
        {
            _meter = null;
            PeakChanged?.Invoke(this, 0f);
        }
    }
}
