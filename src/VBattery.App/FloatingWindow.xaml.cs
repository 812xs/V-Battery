using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VBattery.App.ViewModels;
using VBattery.Settings;

namespace VBattery.App;

/// <summary>Sürüklenebilir, isteğe bağlı saydam masaüstü pil göstergesi.</summary>
public partial class FloatingWindow : Window
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly SettingsService _settings;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(15) };

    public FloatingWindow(MainViewModel viewModel, SettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        _clock.Tick += (_, _) => UpdateClock();
        IsVisibleChanged += (_, _) => UpdateClockTimer();
        Apply();
    }

    /// <summary>Ayarları pencereye uygular (Topmost, arka plan, ölçek, saat, konum).</summary>
    public void Apply()
    {
        var s = _settings.Current;
        Topmost = s.FloatingAlwaysOnTop;
        Root.Background = s.FloatingDarkBackground
            ? new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1A))
            : Brushes.Transparent;
        Root.BorderBrush = s.FloatingDarkBackground ? new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x35)) : Brushes.Transparent;
        Root.BorderThickness = new Thickness(1);
        Scale.ScaleX = Scale.ScaleY = s.IconScale;
        ClockText.Visibility = s.FloatingShowClock ? Visibility.Visible : Visibility.Collapsed;
        UpdateClock();
        UpdateClockTimer();
        ApplyPosition(s);
    }

    private void ApplyPosition(AppSettings s)
    {
        var work = SystemParameters.WorkArea;
        var left = s.FloatingLeft ?? work.Right - 280;
        var top = s.FloatingTop ?? work.Bottom - 200;

        // Kayıtlı konum artık görünür ekranda değilse (ekran çıkarılmış) varsayılana dön.
        var vLeft = SystemParameters.VirtualScreenLeft;
        var vTop = SystemParameters.VirtualScreenTop;
        var vRight = vLeft + SystemParameters.VirtualScreenWidth;
        var vBottom = vTop + SystemParameters.VirtualScreenHeight;
        if (left < vLeft - 20 || left > vRight - 40 || top < vTop - 20 || top > vBottom - 40)
        {
            left = work.Right - 280;
            top = work.Bottom - 200;
        }

        Left = left;
        Top = top;
    }

    private void UpdateClock()
    {
        if (_settings.Current.FloatingShowClock)
            ClockText.Text = DateTime.Now.ToString("HH:mm  ·  d MMM ddd", Tr);
    }

    private void UpdateClockTimer()
    {
        if (IsVisible && _settings.Current.FloatingShowClock) _clock.Start();
        else _clock.Stop();
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove(); // fare bırakılana kadar bloklar
        }
        catch (InvalidOperationException)
        {
            return; // fare düğmesi sürükleme başlamadan bırakıldı
        }

        var left = Left;
        var top = Top;
        _settings.Update(s =>
        {
            s.FloatingLeft = left;
            s.FloatingTop = top;
        });
    }
}
