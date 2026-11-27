using System.Text.Json;
using VBattery.Core;

namespace VBattery.Settings;

/// <summary>JSON ayar dosyası (%APPDATA%\V-Battery\settings.json). Kayıt ertelenir ve atomiktir.</summary>
public sealed class SettingsService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Timer? _saveTimer;
    private bool _dirty;

    public SettingsService(string? path = null)
    {
        _path = path ?? DefaultPath;
        Current = Load();
        Sanitize(Current);
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "V-Battery", "settings.json");

    public string FilePath => _path;

    public AppSettings Current { get; private set; }

    /// <summary>Ayar değiştiğinde tetiklenir (değiştiren iş parçacığında).</summary>
    public event EventHandler? Changed;

    public void Update(Action<AppSettings> mutate)
    {
        lock (_lock)
        {
            mutate(Current);
            Sanitize(Current);
            ScheduleSave();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public DeviceSettings GetDevice(string id)
    {
        lock (_lock)
        {
            return Current.Devices.TryGetValue(id, out var d)
                ? new DeviceSettings { Hidden = d.Hidden, NotificationsEnabled = d.NotificationsEnabled }
                : new DeviceSettings();
        }
    }

    public void UpdateDevice(string id, Action<DeviceSettings> mutate) =>
        Update(s =>
        {
            if (!s.Devices.TryGetValue(id, out var d)) s.Devices[id] = d = new DeviceSettings();
            mutate(d);
        });

    public void SaveNow()
    {
        lock (_lock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            WriteFile();
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Settings", ex, "Ayar dosyası okunamadı, varsayılanlar kullanılıyor");
            try { File.Copy(_path, _path + ".bozuk", true); } catch { /* yedek alınamadı */ }
        }
        return new AppSettings();
    }

    private static void Sanitize(AppSettings s)
    {
        s.LowBatteryThreshold = Math.Clamp(s.LowBatteryThreshold, 6, 50);
        s.CriticalBatteryThreshold = Math.Clamp(s.CriticalBatteryThreshold, 1, 5);
        if (s.CriticalBatteryThreshold >= s.LowBatteryThreshold) s.CriticalBatteryThreshold = 5;
        s.IconScale = Math.Clamp(s.IconScale, 0.5, 2.0);
        s.Devices ??= new Dictionary<string, DeviceSettings>();
        if (s.FloatingLeft is double l && !double.IsFinite(l)) s.FloatingLeft = null;
        if (s.FloatingTop is double t && !double.IsFinite(t)) s.FloatingTop = null;
    }

    // _lock altında çağrılır.
    private void ScheduleSave()
    {
        _dirty = true;
        _saveTimer ??= new Timer(_ => { lock (_lock) { _saveTimer?.Dispose(); _saveTimer = null; WriteFile(); } });
        _saveTimer.Change(500, Timeout.Infinite);
    }

    // _lock altında çağrılır.
    private void WriteFile()
    {
        if (!_dirty) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(tmp, _path, overwrite: true);
            _dirty = false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Settings", ex, "Ayarlar kaydedilemedi");
        }
    }

    public void Dispose() => SaveNow();
}
