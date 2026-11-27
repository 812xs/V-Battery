using System.ComponentModel;

namespace VBattery.Core;

public enum DeviceType { Unknown, Mouse, Keyboard, Headset, Gamepad, Other }

public enum ConnectionType { Unknown, Bluetooth, Usb, Wireless }

public enum ChargeState { Unknown, Discharging, Charging }

/// <summary>
/// Yalnızca yüzde vermeyen kaynaklar (ör. XInput) için kaba pil bandı.
/// Yüzde üretmek için kullanılmaz; sadece cihazın bildirdiği seviyeyi taşır.
/// </summary>
public enum BatteryBand { Unknown, Empty, Low, Medium, Full }

/// <summary>Bir sağlayıcının bir cihaz için bildirdiği değişmez durum.</summary>
public sealed record DeviceReading(
    string Id,
    string Name,
    DeviceType Type,
    ConnectionType Connection,
    bool IsConnected,
    int? BatteryPercent = null,
    ChargeState Charge = ChargeState.Unknown,
    bool BatterySupported = true,
    BatteryBand Band = BatteryBand.Unknown);

/// <summary>Algılanan bir cihazın güncel durumu. İş parçacığı güvenlidir.</summary>
public sealed class DeviceInfo : INotifyPropertyChanged
{
    private readonly object _gate = new();
    private volatile DeviceReading _reading;
    private DateTime? _lastUpdated;

    public DeviceInfo(string id)
    {
        Id = id;
        _reading = new DeviceReading(id, "", DeviceType.Unknown, ConnectionType.Unknown, false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Name => string.IsNullOrWhiteSpace(_reading.Name) ? "Bilinmeyen cihaz" : _reading.Name;
    public DeviceType Type => _reading.Type;
    public ConnectionType Connection => _reading.Connection;
    public bool IsConnected => _reading.IsConnected;
    public int? BatteryPercent => _reading.BatteryPercent;
    public ChargeState Charge => _reading.Charge;
    public bool BatterySupported => _reading.BatterySupported;
    public BatteryBand Band => _reading.Band;

    public DateTime? LastUpdated
    {
        get { lock (_gate) return _lastUpdated; }
    }

    /// <summary>Pil değeri okunabiliyor mu (yüzde veya kaba seviye).</summary>
    public bool HasBatteryData =>
        IsConnected && BatterySupported && (BatteryPercent.HasValue || Band != BatteryBand.Unknown);

    /// <summary>Gösterilecek metin; bilgi yoksa "--".</summary>
    public string BatteryText
    {
        get
        {
            if (!HasBatteryData) return "--";
            if (BatteryPercent is int p) return $"{p}%";
            return Band switch
            {
                BatteryBand.Empty => "Boş",
                BatteryBand.Low => "Düşük",
                BatteryBand.Medium => "Orta",
                BatteryBand.Full => "Dolu",
                _ => "--"
            };
        }
    }

    /// <summary>Okumayı uygular. Görünür bir alan değiştiyse true döner.</summary>
    public bool Apply(DeviceReading reading)
    {
        bool changed;
        lock (_gate)
        {
            changed = !_reading.Equals(reading);
            _reading = reading;
            if (reading.IsConnected && reading.BatterySupported &&
                (reading.BatteryPercent.HasValue || reading.Band != BatteryBand.Unknown))
            {
                _lastUpdated = DateTime.Now;
            }
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        return changed;
    }
}
