using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using VBattery.Core;

namespace VBattery.Tray;

/// <summary>
/// Tepsi simgesini dinamik çizer: üstte cihaz türü simgesi, altta pil yüzdesi.
/// Düşük pilde kırmızı, şarjda yeşil + şimşek; bilgi yoksa "--".
/// </summary>
public static class TrayIconRenderer
{
    private static readonly Color Background = Color.FromArgb(235, 22, 22, 28);
    private static readonly Color Normal = Color.FromArgb(255, 236, 236, 241);
    private static readonly Color Low = Color.FromArgb(255, 255, 69, 58);
    private static readonly Color Charging = Color.FromArgb(255, 48, 209, 88);
    private static readonly Color Accent = Color.FromArgb(255, 140, 148, 255);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static bool IsLow(DeviceInfo d, int lowThreshold) =>
        d.HasBatteryData &&
        (d.BatteryPercent is int p ? p <= lowThreshold : d.Band is BatteryBand.Low or BatteryBand.Empty);

    public static string ShortText(DeviceInfo d)
    {
        if (!d.HasBatteryData) return "--";
        if (d.BatteryPercent is int p) return p.ToString();
        return d.Band switch
        {
            BatteryBand.Empty => "Boş",
            BatteryBand.Low => "Az",
            BatteryBand.Medium => "Orta",
            BatteryBand.Full => "Dolu",
            _ => "--"
        };
    }

    /// <summary>Çağıran, dönen simgeyi Dispose etmekle yükümlüdür.</summary>
    public static Icon Render(DeviceInfo device, int size, int lowThreshold)
    {
        size = Math.Max(16, size);
        var low = IsLow(device, lowThreshold);
        var charging = device.Charge == ChargeState.Charging;
        var foreground = low ? Low : charging ? Charging : Normal;
        var text = ShortText(device);

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using (var path = RoundedRect(0, 0, size, size, size * 0.24f))
            using (var bg = new SolidBrush(Background))
            {
                g.FillPath(bg, path);
            }

            var glyphHeight = size * 0.40f;
            using (var glyphFont = new Font("Segoe MDL2 Assets", glyphHeight, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var glyphBrush = new SolidBrush(low ? Low : Accent))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(DeviceGlyphs.For(device.Type), glyphFont, glyphBrush,
                    new RectangleF(0, size * 0.04f, size, size * 0.44f), fmt);
            }

            var textPx = text.Length switch { <= 2 => size * 0.50f, 3 => size * 0.40f, _ => size * 0.30f };
            using (var font = new Font("Segoe UI", textPx, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(foreground))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, font, brush, new RectangleF(0, size * 0.46f, size, size * 0.54f), fmt);
            }

            if (charging)
            {
                var s = size / 16f;
                var bolt = new[]
                {
                    new PointF(13.2f * s, 1.0f * s), new PointF(10.2f * s, 5.0f * s), new PointF(12.0f * s, 5.0f * s),
                    new PointF(9.6f * s, 9.0f * s), new PointF(14.0f * s, 4.2f * s), new PointF(12.0f * s, 4.2f * s),
                };
                using var boltBrush = new SolidBrush(Charging);
                g.FillPolygon(boltBrush, bolt);
            }
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone(); // kopya kendi tutamacını taşır; geçici tutamaç aşağıda yok edilir
        }
        finally
        {
            DestroyIcon(hIcon); // GDI sızıntısını önler
        }
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var d = r * 2;
        var p = new GraphicsPath();
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
