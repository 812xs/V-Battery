using VBattery.Core;
using VBattery.DeviceDetection;
using VBattery.Notifications;
using VBattery.Settings;

namespace VBattery.Tray;

/// <summary>Tepsi menüsünün çağırdığı uygulama eylemleri.</summary>
public sealed record TrayActions(
    Action OpenMainWindow,
    Action RefreshNow,
    Action OpenDiagnostics,
    Action ShowAbout,
    Action TestNotification,
    Action Exit);

/// <summary>
/// Her cihaz için ayrı NotifyIcon + her zaman var olan V-Battery simgesi (menü sahibi).
/// UI iş parçacığında oluşturulmalıdır.
/// </summary>
public sealed class TrayManager : INotifier, IDisposable
{
    private sealed class DeviceIcon : IDisposable
    {
        public required NotifyIcon Notify { get; init; }
        public Icon? Icon;
        public string Key = "";

        public void Dispose()
        {
            Notify.Visible = false;
            Notify.Dispose();
            Icon?.Dispose();
        }
    }

    private static readonly (string Label, double Scale)[] Sizes =
    {
        ("Küçük", 0.75), ("Normal", 1.0), ("Büyük", 1.25), ("Çok büyük", 1.5)
    };

    private readonly SettingsService _settings;
    private readonly DeviceMonitor _monitor;
    private readonly TrayActions _actions;
    private readonly SynchronizationContext _ui;
    private readonly Dictionary<string, DeviceIcon> _icons = new();
    private readonly NotifyIcon _main;
    private readonly ContextMenuStrip _menu = new();
    private int _syncPending;
    private bool _disposed;

    public TrayManager(SettingsService settings, DeviceMonitor monitor, TrayActions actions, Icon? appIcon)
    {
        _settings = settings;
        _monitor = monitor;
        _actions = actions;
        _ui = SynchronizationContext.Current
              ?? throw new InvalidOperationException("TrayManager UI iş parçacığında oluşturulmalıdır.");

        _menu.Opening += (_, _) => BuildMenu();
        _main = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = "V-Battery",
            ContextMenuStrip = _menu
        };
        _main.MouseClick += OnMouseClick;
    }

    public void Start()
    {
        _main.Visible = true;
        _monitor.DeviceUpdated += OnDeviceChanged;
        _monitor.DeviceListChanged += OnListChanged;
        _settings.Changed += OnSettingsChanged;
        Sync();
    }

    // ---- INotifier ----
    public void Show(string title, string message, NotificationKind kind)
    {
        if (_disposed) return;
        _ui.Post(_ =>
        {
            if (_disposed) return;
            var icon = kind switch
            {
                NotificationKind.Critical => ToolTipIcon.Error,
                NotificationKind.Low => ToolTipIcon.Warning,
                _ => ToolTipIcon.Info
            };
            _main.ShowBalloonTip(8000, title, message, icon);
        }, null);
    }

    // ---- Eşitleme ----
    private void OnDeviceChanged(object? s, DeviceInfo d) => RequestSync();
    private void OnListChanged(object? s, EventArgs e) => RequestSync();
    private void OnSettingsChanged(object? s, EventArgs e) => RequestSync();

    private void RequestSync()
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _syncPending, 1) == 0)
        {
            _ui.Post(_ =>
            {
                Interlocked.Exchange(ref _syncPending, 0);
                if (!_disposed) Sync();
            }, null);
        }
    }

    private void Sync()
    {
        try
        {
            var s = _settings.Current;
            var wanted = s.ShowInTray
                ? _monitor.Devices.Where(d => d.IsConnected && !_settings.GetDevice(d.Id).Hidden).ToList()
                : new List<DeviceInfo>();

            var wantedIds = wanted.Select(d => d.Id).ToHashSet();
            foreach (var id in _icons.Keys.Where(k => !wantedIds.Contains(k)).ToList())
            {
                _icons[id].Dispose();
                _icons.Remove(id);
            }

            var px = Math.Max(16, SystemInformation.SmallIconSize.Width);
            foreach (var d in wanted)
            {
                if (!_icons.TryGetValue(d.Id, out var di))
                {
                    di = new DeviceIcon { Notify = new NotifyIcon { ContextMenuStrip = _menu } };
                    di.Notify.MouseClick += OnMouseClick;
                    _icons[d.Id] = di;
                }

                var low = TrayIconRenderer.IsLow(d, s.LowBatteryThreshold);
                var key = $"{d.BatteryPercent}|{d.Band}|{d.Charge}|{low}|{d.Type}|{px}";
                if (key != di.Key)
                {
                    var newIcon = TrayIconRenderer.Render(d, px, s.LowBatteryThreshold);
                    di.Notify.Icon = newIcon;
                    di.Icon?.Dispose();
                    di.Icon = newIcon;
                    di.Key = key;
                }

                var text = $"{d.Name} — {d.BatteryText}" + (d.Charge == ChargeState.Charging ? " (şarj oluyor)" : "");
                di.Notify.Text = text.Length > 120 ? text[..120] : text;
                di.Notify.Visible = true;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Tray", ex, "Tepsi simgeleri güncellenemedi");
        }
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) Run(_actions.OpenMainWindow);
    }

    // ---- Menü ----
    private void BuildMenu()
    {
        var s = _settings.Current;
        foreach (ToolStripItem item in _menu.Items) item.Dispose();
        _menu.Items.Clear();

        // Cihazlar
        var devicesItem = new ToolStripMenuItem("Cihazlar");
        var all = _monitor.Devices;
        if (all.Count == 0)
        {
            devicesItem.DropDownItems.Add(new ToolStripMenuItem("Cihaz bulunamadı") { Enabled = false });
        }
        else
        {
            foreach (var d in all)
            {
                var id = d.Id;
                var hidden = _settings.GetDevice(id).Hidden;
                var label = $"{d.Name} — {(d.IsConnected ? d.BatteryText : "bağlı değil")}";
                devicesItem.DropDownItems.Add(Item(label, () => _settings.UpdateDevice(id, x => x.Hidden = !x.Hidden), check: !hidden));
            }
        }
        _menu.Items.Add(devicesItem);

        _menu.Items.Add(Item("Görev çubuğunda göster", () => _settings.Update(x => x.ShowInTray = !x.ShowInTray), check: s.ShowInTray));
        _menu.Items.Add(Item("Masaüstünde serbest göster", () => _settings.Update(x => x.FloatingEnabled = !x.FloatingEnabled), check: s.FloatingEnabled));

        var sizeItem = new ToolStripMenuItem("Simge boyutu");
        sizeItem.DropDownItems.Add(new ToolStripMenuItem("Masaüstü göstergesine uygulanır") { Enabled = false });
        foreach (var (label, scale) in Sizes)
        {
            var sc = scale;
            sizeItem.DropDownItems.Add(Item(label, () => _settings.Update(x => x.IconScale = sc), check: Math.Abs(s.IconScale - sc) < 0.01));
        }
        _menu.Items.Add(sizeItem);

        _menu.Items.Add(Item("Saat ve tarihi göster", () => _settings.Update(x => x.FloatingShowClock = !x.FloatingShowClock), check: s.FloatingShowClock));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Düşük pil bildirimi", () => _settings.Update(x => x.LowBatteryNotifications = !x.LowBatteryNotifications), check: s.LowBatteryNotifications));
        _menu.Items.Add(Item("Şarj dolunca bildir", () => _settings.Update(x => x.ChargeCompleteNotifications = !x.ChargeCompleteNotifications), check: s.ChargeCompleteNotifications));
        _menu.Items.Add(Item("Bildirimi test et", _actions.TestNotification));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Windows ile başlat", () => _settings.Update(x => x.AutoStart = !x.AutoStart), check: s.AutoStart));
        _menu.Items.Add(Item("Şimdi yenile", _actions.RefreshNow));
        _menu.Items.Add(Item("Tanılama raporunu aç", _actions.OpenDiagnostics));
        _menu.Items.Add(Item("Hakkında", _actions.ShowAbout));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Çıkış", _actions.Exit));
    }

    private static ToolStripMenuItem Item(string text, Action onClick, bool? check = null)
    {
        var item = new ToolStripMenuItem(text);
        if (check.HasValue) item.Checked = check.Value;
        item.Click += (_, _) => Run(onClick);
        return item;
    }

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { DiagnosticLog.Write("Tray", ex, "Menü eylemi başarısız"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.DeviceUpdated -= OnDeviceChanged;
        _monitor.DeviceListChanged -= OnListChanged;
        _settings.Changed -= OnSettingsChanged;
        foreach (var i in _icons.Values) i.Dispose();
        _icons.Clear();
        _main.Visible = false;
        _main.Dispose();
        _menu.Dispose();
    }
}
