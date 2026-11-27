namespace VBattery.Core;

public static class DeviceTypeGuesser
{
    private static readonly string[] KeyboardWords = { "keyboard", "klavye", "keys", "keychron" };
    private static readonly string[] MouseWords = { "mouse", "fare", "trackball", "trackpad", "mx master", "mx anywhere" };
    private static readonly string[] HeadsetWords =
    {
        "headset", "headphone", "kulaklık", "kulaklik", "earbud", "buds", "airpods", "earphone",
        "arctis", "wh-1000", "wf-1000", "soundcore", "jabra", "bose", "speaker"
    };
    private static readonly string[] GamepadWords =
    {
        "gamepad", "controller", "joy-con", "joycon", "dualshock", "dualsense", "xbox", "wireless controller", "joystick"
    };

    /// <summary>İsimden tür tahmini. Yalnızca simge/etiket seçimi içindir, pil değeriyle ilgisi yoktur.</summary>
    public static DeviceType FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return DeviceType.Unknown;
        var n = name.ToLowerInvariant();
        if (GamepadWords.Any(n.Contains)) return DeviceType.Gamepad;
        if (HeadsetWords.Any(n.Contains)) return DeviceType.Headset;
        if (KeyboardWords.Any(n.Contains)) return DeviceType.Keyboard;
        if (MouseWords.Any(n.Contains)) return DeviceType.Mouse;
        return DeviceType.Unknown;
    }
}

/// <summary>Segoe MDL2 Assets karakterleri (Windows 10/11'de yüklü).</summary>
public static class DeviceGlyphs
{
    public static string For(DeviceType type) => type switch
    {
        DeviceType.Mouse => "",
        DeviceType.Keyboard => "",
        DeviceType.Headset => "",
        DeviceType.Gamepad => "",
        _ => ""
    };

    public static string TypeName(DeviceType type) => type switch
    {
        DeviceType.Mouse => "Mouse",
        DeviceType.Keyboard => "Klavye",
        DeviceType.Headset => "Kulaklık",
        DeviceType.Gamepad => "Gamepad",
        DeviceType.Other => "Diğer",
        _ => "Bilinmiyor"
    };

    public static string ConnectionName(ConnectionType c) => c switch
    {
        ConnectionType.Bluetooth => "Bluetooth",
        ConnectionType.Usb => "USB",
        ConnectionType.Wireless => "Kablosuz",
        _ => "Bilinmiyor"
    };
}
