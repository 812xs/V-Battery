namespace VBattery.Core;

/// <summary>
/// Cihaz keşif/pil okuma protokolü sağlayıcısı. Yeni protokoller (ör. üretici özel HID++)
/// bu arayüzü uygulayıp <c>DeviceMonitor.AddProvider</c> ile eklenir.
/// </summary>
public interface IDeviceProvider : IDisposable
{
    string Name { get; }

    /// <summary>Bir cihaz için yeni/güncel durum olduğunda tetiklenir (herhangi bir iş parçacığı).</summary>
    event EventHandler<DeviceReading>? ReadingChanged;

    /// <summary>Sağlayıcıyı başlatır. Hızlı dönmeli; uzun işler arka planda yürümeli.</summary>
    Task StartAsync();

    /// <summary>Bilinen cihazları hemen yeniden okur.</summary>
    Task RefreshAsync();
}
