namespace VBattery.Settings;

public sealed class DeviceSettings
{
    /// <summary>Göstergelerden (tepsi + masaüstü) gizle.</summary>
    public bool Hidden { get; set; }

    /// <summary>Bu cihaz için bildirimler açık mı.</summary>
    public bool NotificationsEnabled { get; set; } = true;
}

public sealed class AppSettings
{
    // Başlangıç
    public bool AutoStart { get; set; } = true;
    public bool StartMinimized { get; set; } = true;

    // Bildirimler
    public bool LowBatteryNotifications { get; set; } = true;
    public int LowBatteryThreshold { get; set; } = 20;      // 10 / 15 / 20 / 30
    public int CriticalBatteryThreshold { get; set; } = 5;
    public bool ChargeCompleteNotifications { get; set; }

    // Tepsi
    public bool ShowInTray { get; set; } = true;

    // Masaüstü göstergesi
    public bool FloatingEnabled { get; set; }
    public bool FloatingAlwaysOnTop { get; set; } = true;
    public bool FloatingDarkBackground { get; set; } = true;
    public bool FloatingShowClock { get; set; }
    public double IconScale { get; set; } = 1.0;            // 0.75 / 1.0 / 1.25 / 1.5
    public double? FloatingLeft { get; set; }
    public double? FloatingTop { get; set; }

    public string Theme { get; set; } = "Dark";

    public Dictionary<string, DeviceSettings> Devices { get; set; } = new();
}
