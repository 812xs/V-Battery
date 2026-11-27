using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using VBattery.App.ViewModels;
using VBattery.Notifications;
using VBattery.Settings;

namespace VBattery.App;

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly BatteryNotificationService _notifications;
    private readonly Action _openDiagnostics;
    private readonly List<Action> _loaders = new();
    private bool _loading;

    public MainWindow(MainViewModel viewModel, SettingsService settings,
        BatteryNotificationService notifications, Action openDiagnostics)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        _notifications = notifications;
        _openDiagnostics = openDiagnostics;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Sürüm {version?.ToString(3) ?? "0.1.0"}";
        SettingsPathText.Text = $"Ayar dosyası: {settings.FilePath}";

        Bind(ChkLow, s => s.LowBatteryNotifications, (s, v) => s.LowBatteryNotifications = v);
        Bind(ChkCharge, s => s.ChargeCompleteNotifications, (s, v) => s.ChargeCompleteNotifications = v);
        Bind(ChkAutoStart, s => s.AutoStart, (s, v) => s.AutoStart = v);
        Bind(ChkStartMin, s => s.StartMinimized, (s, v) => s.StartMinimized = v);
        Bind(ChkTray, s => s.ShowInTray, (s, v) => s.ShowInTray = v);
        Bind(ChkFloating, s => s.FloatingEnabled, (s, v) => s.FloatingEnabled = v);
        Bind(ChkTop, s => s.FloatingAlwaysOnTop, (s, v) => s.FloatingAlwaysOnTop = v);
        Bind(ChkDark, s => s.FloatingDarkBackground, (s, v) => s.FloatingDarkBackground = v);
        Bind(ChkClock, s => s.FloatingShowClock, (s, v) => s.FloatingShowClock = v);
        BindSegments(ThresholdPanel, s => s.LowBatteryThreshold, (s, v) => s.LowBatteryThreshold = (int)Math.Round(v));
        BindSegments(ScalePanel, s => s.IconScale, (s, v) => s.IconScale = v);

        _settings.Changed += OnSettingsChanged;
        LoadUi();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(LoadUi));

    private void LoadUi()
    {
        _loading = true;
        try { foreach (var load in _loaders) load(); }
        finally { _loading = false; }
    }

    private void Bind(CheckBox box, Func<AppSettings, bool> get, Action<AppSettings, bool> set)
    {
        _loaders.Add(() => box.IsChecked = get(_settings.Current));
        box.Click += (_, _) =>
        {
            if (_loading) return;
            var value = box.IsChecked == true;
            _settings.Update(s => set(s, value));
        };
    }

    private void BindSegments(Panel panel, Func<AppSettings, double> get, Action<AppSettings, double> set)
    {
        var buttons = panel.Children.OfType<RadioButton>().ToList();
        _loaders.Add(() =>
        {
            var current = get(_settings.Current);
            foreach (var rb in buttons)
                rb.IsChecked = Math.Abs(double.Parse((string)rb.Tag, CultureInfo.InvariantCulture) - current) < 0.001;
        });
        foreach (var rb in buttons)
        {
            rb.Click += (_, _) =>
            {
                if (_loading) return;
                var value = double.Parse((string)rb.Tag, CultureInfo.InvariantCulture);
                _settings.Update(s => set(s, value));
            };
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageStatus == null) return; // InitializeComponent sırasında
        var index = int.Parse((string)((RadioButton)sender).Tag, CultureInfo.InvariantCulture);
        PageStatus.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageDevices.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageNotifications.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e) => _notifications.SendTest();

    private void BtnDiag_Click(object sender, RoutedEventArgs e) => _openDiagnostics();

    protected override void OnClosing(CancelEventArgs e)
    {
        // Pencereyi kapatmak uygulamayı kapatmaz; tepsiye gizler. Çıkış tepsi menüsündendir.
        if (!((App)Application.Current).IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _settings.Changed -= OnSettingsChanged;
        base.OnClosing(e);
    }
}
