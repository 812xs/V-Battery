using Microsoft.Win32;

namespace VBattery.Settings;

/// <summary>Windows ile başlatma: HKCU\...\Run kaydı (yönetici izni gerekmez).</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "V-Battery";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    public static void SetEnabled(bool enabled, string? exePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var path = exePath ?? Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) return;
            key.SetValue(ValueName, $"\"{path}\" --minimized", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
