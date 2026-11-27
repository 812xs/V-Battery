using System.ComponentModel;
using System.Windows.Media;
using VBattery.Core;
using VBattery.Settings;

namespace VBattery.App.ViewModels;

public sealed class DeviceViewModel : INotifyPropertyChanged
{
    private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7C, 0x83, 0xFF)));
    private static readonly Brush LowBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));
    private static readonly Brush ChargeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)));
    private static readonly Brush DimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xAB)));

    private readonly SettingsService _settings;

    public DeviceViewModel(DeviceInfo device, SettingsService settings)
    {
        Device = device;
        _settings = settings;
        device.PropertyChanged += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceInfo Device { get; }

    public string Id => Device.Id;
    public string Name => Device.Name;
    public string Glyph => DeviceGlyphs.For(Device.Type);
    public string BatteryText => Device.BatteryText;
    public bool IsConnected => Device.IsConnected;
    public bool HasPercent => Device.BatteryPercent.HasValue;
    public double Percent => Device.BatteryPercent ?? 0;

    public string TypeText => DeviceGlyphs.TypeName(Device.Type);

    public string StatusText
    {
        get
        {
            if (!Device.IsConnected) return "Bağlı değil";
            var conn = DeviceGlyphs.ConnectionName(Device.Connection);
            if (!Device.BatterySupported) return $"{conn} · Pil bilgisi sunulmuyor";
            if (!Device.HasBatteryData) return $"{conn} · Pil bilgisi alınamadı";
            return Device.Charge == ChargeState.Charging ? $"{conn} · Şarj oluyor" : $"{conn} · Bağlı";
        }
    }

    public string DetailText
    {
        get
        {
            var updated = Device.LastUpdated is DateTime t ? $"Son güncelleme {t:HH:mm}" : "Henüz okunmadı";
            return $"{TypeText} · {updated}";
        }
    }

    public Brush BarBrush
    {
        get
        {
            if (!Device.HasBatteryData) return DimBrush;
            var low = _settings.Current.LowBatteryThreshold;
            var isLow = Device.BatteryPercent is int p ? p <= low : Device.Band is BatteryBand.Low or BatteryBand.Empty;
            if (isLow) return LowBrush;
            return Device.Charge == ChargeState.Charging ? ChargeBrush : AccentBrush;
        }
    }

    public bool IsShown
    {
        get => !_settings.GetDevice(Id).Hidden;
        set => _settings.UpdateDevice(Id, d => d.Hidden = !value);
    }

    public bool NotificationsEnabled
    {
        get => _settings.GetDevice(Id).NotificationsEnabled;
        set => _settings.UpdateDevice(Id, d => d.NotificationsEnabled = value);
    }

    public bool IsVisibleInIndicator => Device.IsConnected && IsShown;

    /// <summary>Tüm özellikleri yeniden bildirir (her iş parçacığından çağrılabilir; bağlama motoru UI'a taşır).</summary>
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
