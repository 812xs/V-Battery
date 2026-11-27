using System.Collections.Concurrent;
using VBattery.Core;

namespace VBattery.DeviceDetection;

/// <summary>
/// Sağlayıcıları toplar, cihazları kimliğe göre tekilleştirir ve değişiklikleri bildirir.
/// Hatalı bir sağlayıcı veya dinleyici uygulamayı çökertmez.
/// </summary>
public sealed class DeviceMonitor : IDisposable
{
    private readonly List<IDeviceProvider> _providers = new();
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Bir cihazın görünür durumu değiştiğinde veya yeni cihaz eklendiğinde (herhangi bir iş parçacığı).</summary>
    public event EventHandler<DeviceInfo>? DeviceUpdated;

    /// <summary>Cihaz listesine yeni cihaz eklendiğinde.</summary>
    public event EventHandler? DeviceListChanged;

    public IReadOnlyList<DeviceInfo> Devices =>
        _devices.Values.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    public IReadOnlyList<string> ProviderNames => _providers.Select(p => p.Name).ToList();

    public void AddProvider(IDeviceProvider provider)
    {
        if (_started) throw new InvalidOperationException("Sağlayıcılar başlatmadan önce eklenmelidir.");
        _providers.Add(provider);
        provider.ReadingChanged += OnReading;
    }

    public async Task StartAsync()
    {
        _started = true;
        foreach (var p in _providers)
        {
            try { await p.StartAsync().ConfigureAwait(false); }
            catch (Exception ex) { DiagnosticLog.Write(p.Name, ex, "Sağlayıcı başlatılamadı"); }
        }
    }

    public async Task RefreshAsync()
    {
        foreach (var p in _providers)
        {
            try { await p.RefreshAsync().ConfigureAwait(false); }
            catch (Exception ex) { DiagnosticLog.Write(p.Name, ex, "Yenileme başarısız"); }
        }
    }

    private void OnReading(object? sender, DeviceReading reading)
    {
        if (_disposed || string.IsNullOrEmpty(reading.Id)) return;
        try
        {
            var isNew = false;
            if (!_devices.TryGetValue(reading.Id, out var device))
            {
                var created = new DeviceInfo(reading.Id);
                device = _devices.GetOrAdd(reading.Id, created);
                isNew = ReferenceEquals(device, created);
            }

            var changed = device.Apply(reading);
            if (isNew) Raise(() => DeviceListChanged?.Invoke(this, EventArgs.Empty));
            if (isNew || changed) Raise(() => DeviceUpdated?.Invoke(this, device));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("DeviceMonitor", ex, "Okuma işlenemedi");
        }
    }

    private static void Raise(Action action)
    {
        try { action(); }
        catch (Exception ex) { DiagnosticLog.Write("DeviceMonitor", ex, "Olay dinleyicisi hata verdi"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var p in _providers)
        {
            p.ReadingChanged -= OnReading;
            try { p.Dispose(); } catch (Exception ex) { DiagnosticLog.Write(p.Name, ex, "Kapatma hatası"); }
        }
    }
}
