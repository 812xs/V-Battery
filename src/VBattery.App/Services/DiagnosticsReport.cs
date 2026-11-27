using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using VBattery.Core;
using VBattery.DeviceDetection;
using VBattery.Settings;

namespace VBattery.App.Services;

/// <summary>Cihaz listesi, ayarlar ve günlükten oluşan düz metin tanılama raporu.</summary>
public static class DiagnosticsReport
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "V-Battery", "diagnostics.txt");

    public static string Build(DeviceMonitor monitor, SettingsService settings)
    {
        var sb = new StringBuilder();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        sb.AppendLine($"V-Battery tanılama raporu — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Sürüm: {version}");
        sb.AppendLine($"İşletim sistemi: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Ayar dosyası: {settings.FilePath}");
        sb.AppendLine($"Sağlayıcılar: {string.Join(", ", monitor.ProviderNames)}");
        sb.AppendLine();

        sb.AppendLine("== Cihazlar ==");
        var devices = monitor.Devices;
        if (devices.Count == 0) sb.AppendLine("(cihaz algılanmadı)");
        foreach (var d in devices)
        {
            sb.AppendLine($"- {d.Name}");
            sb.AppendLine($"    Kimlik: {d.Id}");
            sb.AppendLine($"    Tür: {d.Type}, Bağlantı: {d.Connection}, Bağlı: {d.IsConnected}");
            sb.AppendLine($"    Pil: {d.BatteryText}, Şarj: {d.Charge}, Pil bilgisi sunuluyor: {d.BatterySupported}");
            sb.AppendLine($"    Son güncelleme: {(d.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-")}");
        }

        sb.AppendLine();
        sb.AppendLine("== Günlük (son kayıtlar) ==");
        foreach (var line in DiagnosticLog.Snapshot()) sb.AppendLine(line);
        return sb.ToString();
    }

    public static void WriteAndOpen(DeviceMonitor monitor, SettingsService settings)
    {
        var path = DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Build(monitor, settings), Encoding.UTF8);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
