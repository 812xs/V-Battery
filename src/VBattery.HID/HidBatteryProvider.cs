using Microsoft.Win32.SafeHandles;
using VBattery.Core;
using static VBattery.HID.NativeHid;

namespace VBattery.HID;

/// <summary>
/// Windows HID API ile pil raporu sunan arayüzleri bulur:
///  - Generic Device Controls (0x06) / Battery Strength (0x20)
///  - Battery System (0x85) / RelativeStateOfCharge (0x0D) ve AbsoluteStateOfCharge (0x0E)
/// HID'de pil için olay yoktur; bu yüzden seyrek aralıkla (varsayılan 3 dk) taranır.
/// Üreticiye özel protokoller (ör. Logitech HID++) desteklenmez.
/// </summary>
public sealed class HidBatteryProvider : IDeviceProvider
{
    private readonly record struct BatteryField(int ReportType, ushort UsagePage, ushort Usage, ushort LinkCollection, byte ReportId, int LogicalMin, int LogicalMax);

    private readonly record struct Known(string Name, DeviceType Type, ConnectionType Connection);

    private static readonly (ushort Page, ushort Usage)[] Targets =
    {
        (0x06, 0x20), // Battery Strength
        (0x85, 0x0D), // RelativeStateOfCharge
        (0x85, 0x0E), // AbsoluteStateOfCharge
    };

    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly Dictionary<string, Known> _known = new(); // _scanGate altında
    private Timer? _timer;
    private bool _disposed;
    private int _lastCount = -1;

    public HidBatteryProvider(TimeSpan interval) => _interval = interval;

    public string Name => "USB/HID";

    public event EventHandler<DeviceReading>? ReadingChanged;

    public Task StartAsync()
    {
        _timer = new Timer(state => _ = ScanAsync(), null, TimeSpan.Zero, _interval);
        return Task.CompletedTask;
    }

    public Task RefreshAsync() => ScanAsync();

    private async Task ScanAsync()
    {
        if (_disposed || !await _scanGate.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            await Task.Run(Scan).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(Name, ex, "HID taraması başarısız");
        }
        finally
        {
            if (!_disposed) _scanGate.Release();
        }
    }

    private void Scan()
    {
        var seen = new HashSet<string>();
        foreach (var path in EnumerateHidPaths())
        {
            try
            {
                var reading = Probe(path);
                if (reading == null || !seen.Add(reading.Id)) continue; // aynı fiziksel cihazın birden çok arayüzü tek kayıt olur
                _known[reading.Id] = new Known(reading.Name, reading.Type, reading.Connection);
                ReadingChanged?.Invoke(this, reading);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write(Name, ex, "HID arayüzü okunamadı");
            }
        }

        foreach (var id in _known.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var k = _known[id];
            _known.Remove(id);
            ReadingChanged?.Invoke(this, new DeviceReading(id, k.Name, k.Type, k.Connection, false));
        }

        if (_lastCount != seen.Count)
        {
            _lastCount = seen.Count;
            DiagnosticLog.Write(Name, $"HID taraması: pil raporu sunan {seen.Count} cihaz.");
        }
    }

    private static DeviceReading? Probe(string path)
    {
        using var handle = Open(path);
        if (handle == null) return null;
        if (!HidD_GetPreparsedData(handle, out var preparsed)) return null;

        try
        {
            if (HidP_GetCaps(preparsed, out var caps) != HIDP_STATUS_SUCCESS) return null;

            var fields = FindFields(preparsed, caps);
            if (fields.Count == 0) return null;

            var percent = ReadPercent(handle, preparsed, caps, fields);
            var ids = GetIds(handle);
            var product = GetProductString(handle);
            var serial = GetSerialString(handle);

            var name = product ?? (ids is { } i ? $"HID cihazı {i.Vid:X4}:{i.Pid:X4}" : "HID cihazı");
            var id = ids is { } v
                ? $"hid:{v.Vid:x4}:{v.Pid:x4}:{(string.IsNullOrEmpty(serial) ? path.ToLowerInvariant() : serial)}"
                : $"hid:{path.ToLowerInvariant()}";

            return new DeviceReading(
                id, name, GuessType(product, caps), GuessConnection(path),
                IsConnected: true,
                BatteryPercent: percent,
                Charge: ChargeState.Unknown,
                BatterySupported: percent.HasValue);
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    private static List<BatteryField> FindFields(IntPtr preparsed, HIDP_CAPS caps)
    {
        var result = new List<BatteryField>();
        // Feature raporu önce: okuması isteğe bağlıdır ve cihazı bekletmez.
        Collect(HidP_Feature, caps.NumberFeatureValueCaps);
        Collect(HidP_Input, caps.NumberInputValueCaps);
        return result;

        void Collect(int type, ushort count)
        {
            if (count == 0) return;
            var arr = new HIDP_VALUE_CAPS[count];
            var len = count;
            if (HidP_GetValueCaps(type, arr, ref len, preparsed) != HIDP_STATUS_SUCCESS) return;

            for (var n = 0; n < len; n++)
            {
                var c = arr[n];
                foreach (var (page, usage) in Targets)
                {
                    if (c.UsagePage != page) continue;
                    var matches = c.IsRange != 0 ? usage >= c.UsageOrMin && usage <= c.UsageMax : c.UsageOrMin == usage;
                    if (matches)
                    {
                        result.Add(new BatteryField(type, page, usage, c.LinkCollection, c.ReportID, c.LogicalMin, c.LogicalMax));
                    }
                }
            }
        }
    }

    private static int? ReadPercent(SafeFileHandle handle, IntPtr preparsed, HIDP_CAPS caps, List<BatteryField> fields)
    {
        foreach (var f in fields)
        {
            var length = f.ReportType == HidP_Input ? caps.InputReportByteLength : caps.FeatureReportByteLength;
            if (length == 0) continue;

            var report = new byte[length];
            report[0] = f.ReportId;
            var ok = f.ReportType == HidP_Input
                ? HidD_GetInputReport(handle, report, length)
                : HidD_GetFeature(handle, report, length);
            if (!ok) continue;

            if (HidP_GetUsageValue(f.ReportType, f.UsagePage, f.LinkCollection, f.Usage, out var value, preparsed, report, length)
                != HIDP_STATUS_SUCCESS) continue;

            var pct = HidBatteryMath.ToPercent(value, f.LogicalMin, f.LogicalMax);
            if (pct.HasValue) return pct;
        }
        return null;
    }

    private static DeviceType GuessType(string? product, HIDP_CAPS caps)
    {
        var byName = DeviceTypeGuesser.FromName(product);
        if (byName != DeviceType.Unknown) return byName;
        if (caps.UsagePage == 0x01) // Generic Desktop
        {
            return caps.Usage switch
            {
                0x02 => DeviceType.Mouse,
                0x06 => DeviceType.Keyboard,
                0x04 or 0x05 => DeviceType.Gamepad,
                _ => DeviceType.Other
            };
        }
        return DeviceType.Other;
    }

    private static ConnectionType GuessConnection(string path)
    {
        var p = path.ToLowerInvariant();
        // Bluetooth HID (klasik) ve HOGP (LE) arayüz yolları bu servis GUID'lerini içerir.
        if (p.Contains("{00001124-0000-1000-8000-00805f9b34fb}") || p.Contains("{00001812-0000-1000-8000-00805f9b34fb}"))
            return ConnectionType.Bluetooth;
        return ConnectionType.Usb;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
