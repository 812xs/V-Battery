using System.Collections.Concurrent;
using VBattery.Core;
using VBattery.Settings;

namespace VBattery.Notifications;

public enum NotificationKind { Info, Low, Critical, ChargeComplete }

/// <summary>Bildirimi gerçekten gösteren taraf (tepsi balonu / toast).</summary>
public interface INotifier
{
    void Show(string title, string message, NotificationKind kind);
}

/// <summary>
/// Eşik mantığı: düşük (%10/15/20/30), kritik (%5) ve şarj tamamlandı.
/// Her seviye için tek seferlik uyarı verir; pil yeterince yükselince (histerezis) yeniden kurulur.
/// </summary>
public sealed class BatteryNotificationService
{
    private sealed class State { public bool LowSent, CriticalSent, FullSent; }

    private readonly SettingsService _settings;
    private readonly INotifier _notifier;
    private readonly ConcurrentDictionary<string, State> _states = new();

    public BatteryNotificationService(SettingsService settings, INotifier notifier)
    {
        _settings = settings;
        _notifier = notifier;
    }

    public void Evaluate(DeviceInfo device)
    {
        try { EvaluateCore(device); }
        catch (Exception ex) { DiagnosticLog.Write("Notifications", ex, "Bildirim değerlendirilemedi"); }
    }

    public void SendTest() =>
        _notifier.Show("V-Battery", "Bildirim testi: bildirimler çalışıyor.", NotificationKind.Info);

    private void EvaluateCore(DeviceInfo d)
    {
        var s = _settings.Current;
        var st = _states.GetOrAdd(d.Id, _ => new State());
        var perDevice = _settings.GetDevice(d.Id);

        // Bağlantı kopmuşsa veya veri yoksa durumu koru, bildirme.
        if (!d.HasBatteryData) return;

        var charging = d.Charge == ChargeState.Charging;
        int? p = d.BatteryPercent;

        var critical = p is int pc ? pc <= s.CriticalBatteryThreshold : d.Band == BatteryBand.Empty;
        var low = p is int pl ? pl <= s.LowBatteryThreshold : d.Band is BatteryBand.Low or BatteryBand.Empty;

        string? title = null, message = null;
        NotificationKind kind = NotificationKind.Info;

        lock (st)
        {
            // Yeniden kurma (histerezis)
            if (charging || (p is int a && a > s.LowBatteryThreshold + 5) || d.Band is BatteryBand.Medium or BatteryBand.Full)
            {
                st.LowSent = false;
            }
            if (charging || (p is int b && b > s.CriticalBatteryThreshold + 3) || d.Band is BatteryBand.Medium or BatteryBand.Full)
            {
                st.CriticalSent = false;
            }
            if (!charging || (p is int c && c < 95)) st.FullSent = false;

            if (perDevice.NotificationsEnabled)
            {
                if (s.LowBatteryNotifications && !charging)
                {
                    if (critical && !st.CriticalSent)
                    {
                        st.CriticalSent = true;
                        st.LowSent = true;
                        title = "V-Battery — Kritik pil";
                        message = p is int pv
                            ? $"{d.Name}: pil %{pv}. Hemen şarj edin."
                            : $"{d.Name}: pil boş. Hemen şarj edin.";
                        kind = NotificationKind.Critical;
                    }
                    else if (low && !critical && !st.LowSent)
                    {
                        st.LowSent = true;
                        title = "V-Battery — Düşük pil";
                        message = p is int pv
                            ? $"{d.Name}: pil %{pv}."
                            : $"{d.Name}: pil seviyesi düşük.";
                        kind = NotificationKind.Low;
                    }
                }

                if (title == null && s.ChargeCompleteNotifications && charging && p is >= 100 && !st.FullSent)
                {
                    st.FullSent = true;
                    title = "V-Battery — Şarj tamamlandı";
                    message = $"{d.Name}: pil %100.";
                    kind = NotificationKind.ChargeComplete;
                }
            }
        }

        if (title != null && message != null) _notifier.Show(title, message, kind);
    }
}
