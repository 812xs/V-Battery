using VBattery.Core;
using VBattery.Notifications;
using VBattery.Settings;
using Xunit;

namespace VBattery.Tests;

public sealed class TempSettings : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vbattery-tests-" + Guid.NewGuid().ToString("N"));
    public string Path_ => Path.Combine(Dir, "settings.json");
    public void Dispose() { try { Directory.Delete(Dir, true); } catch { } }
}

public class SettingsServiceTests
{
    [Fact]
    public void Settings_survive_restart()
    {
        using var t = new TempSettings();
        using (var s = new SettingsService(t.Path_))
        {
            s.Update(x =>
            {
                x.LowBatteryThreshold = 30;
                x.FloatingLeft = 120.5;
                x.AutoStart = false;
            });
            s.UpdateDevice("ble:1", d => d.Hidden = true);
            s.SaveNow();
        }

        using var again = new SettingsService(t.Path_);
        Assert.Equal(30, again.Current.LowBatteryThreshold);
        Assert.Equal(120.5, again.Current.FloatingLeft);
        Assert.False(again.Current.AutoStart);
        Assert.True(again.GetDevice("ble:1").Hidden);
        Assert.False(again.GetDevice("other").Hidden);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        using var t = new TempSettings();
        Directory.CreateDirectory(t.Dir);
        File.WriteAllText(t.Path_, "{ bozuk json");
        using var s = new SettingsService(t.Path_);
        Assert.Equal(20, s.Current.LowBatteryThreshold);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        using var t = new TempSettings();
        using var s = new SettingsService(t.Path_);
        s.Update(x => { x.LowBatteryThreshold = 999; x.IconScale = 50; });
        Assert.InRange(s.Current.LowBatteryThreshold, 6, 50);
        Assert.InRange(s.Current.IconScale, 0.5, 2.0);
    }
}

public class NotificationTests
{
    private sealed class FakeNotifier : INotifier
    {
        public List<(string Title, string Message, NotificationKind Kind)> Shown { get; } = new();
        public void Show(string title, string message, NotificationKind kind) => Shown.Add((title, message, kind));
    }

    private static (BatteryNotificationService Svc, FakeNotifier Notifier, SettingsService Settings, TempSettings Temp) Create()
    {
        var temp = new TempSettings();
        var settings = new SettingsService(temp.Path_);
        var notifier = new FakeNotifier();
        return (new BatteryNotificationService(settings, notifier), notifier, settings, temp);
    }

    private static DeviceInfo Device(int? percent, ChargeState charge = ChargeState.Discharging, string id = "d1")
    {
        var d = new DeviceInfo(id);
        d.Apply(new DeviceReading(id, "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, true, percent, charge));
        return d;
    }

    private static void Set(DeviceInfo d, int percent, ChargeState charge = ChargeState.Discharging) =>
        d.Apply(new DeviceReading(d.Id, "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, true, percent, charge));

    [Fact]
    public void Low_battery_notifies_once()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        var d = Device(19);
        svc.Evaluate(d);
        svc.Evaluate(d);
        Assert.Single(n.Shown);
        Assert.Equal(NotificationKind.Low, n.Shown[0].Kind);
    }

    [Fact]
    public void Above_threshold_does_not_notify()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        svc.Evaluate(Device(21));
        Assert.Empty(n.Shown);
    }

    [Fact]
    public void Critical_at_five_percent()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        var d = Device(20);
        svc.Evaluate(d);
        Set(d, 5);
        svc.Evaluate(d);
        Assert.Equal(new[] { NotificationKind.Low, NotificationKind.Critical }, n.Shown.Select(x => x.Kind).ToArray());
    }

    [Fact]
    public void Rearms_after_recovery_with_hysteresis()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        var d = Device(15);
        svc.Evaluate(d);
        Set(d, 22); // eşik+5 üstünde değil: yeniden kurulmaz
        svc.Evaluate(d);
        Set(d, 18);
        svc.Evaluate(d);
        Assert.Single(n.Shown);

        Set(d, 40); // yeterince yükseldi
        svc.Evaluate(d);
        Set(d, 15);
        svc.Evaluate(d);
        Assert.Equal(2, n.Shown.Count);
    }

    [Fact]
    public void Per_device_switch_silences_notifications()
    {
        var (svc, n, s, t) = Create();
        using var cleanup = t;
        s.UpdateDevice("d1", x => x.NotificationsEnabled = false);
        svc.Evaluate(Device(3));
        Assert.Empty(n.Shown);
    }

    [Fact]
    public void Charging_device_is_not_warned_about_low_battery()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        svc.Evaluate(Device(4, ChargeState.Charging));
        Assert.Empty(n.Shown);
    }

    [Fact]
    public void Charge_complete_is_optional_and_sent_once()
    {
        var (svc, n, s, t) = Create();
        using var cleanup = t;
        var d = Device(100, ChargeState.Charging);
        svc.Evaluate(d);
        Assert.Empty(n.Shown); // varsayılan kapalı

        s.Update(x => x.ChargeCompleteNotifications = true);
        svc.Evaluate(d);
        svc.Evaluate(d);
        Assert.Single(n.Shown);
        Assert.Equal(NotificationKind.ChargeComplete, n.Shown[0].Kind);
    }

    [Fact]
    public void Disabling_low_battery_notifications_silences_low_and_critical()
    {
        var (svc, n, s, t) = Create();
        using var cleanup = t;
        s.Update(x => x.LowBatteryNotifications = false);
        svc.Evaluate(Device(2));
        Assert.Empty(n.Shown);
    }

    [Fact]
    public void Disconnected_device_never_notifies()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        var d = new DeviceInfo("d1");
        d.Apply(new DeviceReading("d1", "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, false, 3));
        svc.Evaluate(d);
        Assert.Empty(n.Shown);
    }

    [Fact]
    public void Coarse_low_band_notifies_without_percent()
    {
        var (svc, n, _, t) = Create();
        using var cleanup = t;
        var d = new DeviceInfo("pad");
        d.Apply(new DeviceReading("pad", "Pad", DeviceType.Gamepad, ConnectionType.Wireless, true, Band: BatteryBand.Low));
        svc.Evaluate(d);
        Assert.Single(n.Shown);
    }
}
