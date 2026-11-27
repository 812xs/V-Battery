using System.Runtime.InteropServices;
using VBattery.Core;

namespace VBattery.HID;

/// <summary>
/// Xbox/XInput uyumlu gamepad'ler için XInputGetBatteryInformation (xinput1_4.dll).
/// Bu API yüzde DEĞİL, dört kademeli seviye (boş/düşük/orta/dolu) verir; uygulama da yalnızca bunu gösterir.
/// </summary>
public sealed class XInputBatteryProvider : IDeviceProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetBatteryInformation(uint userIndex, byte devType, out XINPUT_BATTERY_INFORMATION info);

    private const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;
    private const byte BATTERY_TYPE_DISCONNECTED = 0x00;
    private const byte BATTERY_TYPE_WIRED = 0x01;
    private const uint ERROR_SUCCESS = 0;

    private readonly TimeSpan _interval;
    private readonly HashSet<int> _present = new();
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _unavailable;
    private bool _disposed;

    public XInputBatteryProvider(TimeSpan interval) => _interval = interval;

    public string Name => "XInput";

    public event EventHandler<DeviceReading>? ReadingChanged;

    public Task StartAsync()
    {
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, _interval);
        return Task.CompletedTask;
    }

    public Task RefreshAsync()
    {
        Poll();
        return Task.CompletedTask;
    }

    private void Poll()
    {
        if (_disposed || _unavailable) return;
        lock (_gate)
        {
            try
            {
                for (var i = 0; i < 4; i++) PollSlot(i);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                _unavailable = true;
                DiagnosticLog.Write(Name, ex, "XInput kullanılamıyor");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write(Name, ex, "XInput sorgusu başarısız");
            }
        }
    }

    private void PollSlot(int index)
    {
        var id = $"xinput:{index}";
        var name = $"Xbox/XInput Gamepad {index + 1}";
        var rc = XInputGetBatteryInformation((uint)index, BATTERY_DEVTYPE_GAMEPAD, out var info);

        if (rc != ERROR_SUCCESS || info.BatteryType == BATTERY_TYPE_DISCONNECTED)
        {
            if (_present.Remove(index))
                ReadingChanged?.Invoke(this, new DeviceReading(id, name, DeviceType.Gamepad, ConnectionType.Wireless, false));
            return;
        }

        _present.Add(index);

        if (info.BatteryType == BATTERY_TYPE_WIRED)
        {
            // Kablolu: pil yok. Sahte değer üretilmez.
            ReadingChanged?.Invoke(this, new DeviceReading(id, name, DeviceType.Gamepad, ConnectionType.Usb, true,
                BatterySupported: false));
            return;
        }

        var band = info.BatteryLevel switch
        {
            0 => BatteryBand.Empty,
            1 => BatteryBand.Low,
            2 => BatteryBand.Medium,
            3 => BatteryBand.Full,
            _ => BatteryBand.Unknown
        };

        ReadingChanged?.Invoke(this, new DeviceReading(id, name, DeviceType.Gamepad, ConnectionType.Wireless, true,
            BatterySupported: band != BatteryBand.Unknown, Band: band));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
