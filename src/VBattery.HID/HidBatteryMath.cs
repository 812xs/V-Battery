namespace VBattery.HID;

public static class HidBatteryMath
{
    /// <summary>
    /// HID mantıksal aralığını (LogicalMin..LogicalMax) yüzdeye çevirir.
    /// Aralık dışı veya geçersiz değerler için null döner (tahmin üretilmez).
    /// </summary>
    public static int? ToPercent(uint value, int logicalMin, int logicalMax)
    {
        if (logicalMax <= logicalMin) return null;
        if (value < logicalMin || value > (uint)logicalMax) return null;
        var pct = (int)Math.Round((value - (double)logicalMin) * 100.0 / (logicalMax - logicalMin));
        return Math.Clamp(pct, 0, 100);
    }
}
