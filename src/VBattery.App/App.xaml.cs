using System.Drawing;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using VBattery.App.Services;
using VBattery.App.ViewModels;
using VBattery.Bluetooth;
using VBattery.Core;
using VBattery.DeviceDetection;
using VBattery.HID;
using VBattery.Notifications;
using VBattery.Settings;
using VBattery.Tray;

namespace VBattery.App;

public partial class App : Application
{
    private const string MutexName = @"Local\V-Battery.SingleInstance";
    private const string ShowEventName = @"Local\V-Battery.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private SettingsService _settings = null!;
    private DeviceMonitor _monitor = null!;
    private MainViewModel _viewModel = null!;
    private BatteryNotificationService _notifications = null!;
    private TrayManager _tray = null!;
    private MainWindow? _mainWindow;
    private FloatingWindow? _floating;

    public bool IsExiting { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!createdNew)
        {
            _showEvent.Set(); // çalışan örneğe ana pencereyi göstermesini söyle
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            DiagnosticLog.Write("App", args.Exception, "Yakalanmamış arayüz hatası");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagnosticLog.Write("App", args.ExceptionObject?.ToString() ?? "bilinmeyen hata");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DiagnosticLog.Write("App", args.Exception, "Gözlenmeyen görev hatası");
            args.SetObserved();
        };

        try
        {
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
                (_, _) => Dispatcher.BeginInvoke(new Action(ShowMainWindow)), null, Timeout.Infinite, false);

            _settings = new SettingsService();
            ApplyStartupSetting();

            _monitor = new DeviceMonitor();
            _monitor.AddProvider(new BluetoothGattBatteryProvider());
            _monitor.AddProvider(new HidBatteryProvider(TimeSpan.FromMinutes(3)));
            _monitor.AddProvider(new XInputBatteryProvider(TimeSpan.FromMinutes(2)));

            _viewModel = new MainViewModel(_monitor, _settings, Dispatcher);

            _tray = new TrayManager(_settings, _monitor,
                new TrayActions(
                    OpenMainWindow: ShowMainWindow,
                    RefreshNow: () => _ = _monitor.RefreshAsync(),
                    OpenDiagnostics: OpenDiagnostics,
                    ShowAbout: ShowAbout,
                    TestNotification: () => _notifications.SendTest(),
                    Exit: ExitApplication),
                LoadAppIcon());
            _notifications = new BatteryNotificationService(_settings, _tray);
            _monitor.DeviceUpdated += (_, d) => _notifications.Evaluate(d);

            _settings.Changed += (_, _) => Dispatcher.BeginInvoke(new Action(ApplySettings));

            _tray.Start();
            ApplySettings();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // Windows başlangıcından gelen (--minimized) örnek tepside başlar.
            var minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
            if (!minimized) ShowMainWindow();

            await _monitor.StartAsync();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("App", ex, "Başlatma hatası");
            MessageBox.Show($"V-Battery başlatılamadı:\n{ex.Message}", "V-Battery", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    private void ApplyStartupSetting()
    {
        try
        {
            // Etkinse her açılışta yolu tazele (uygulama taşınmış olabilir).
            if (_settings.Current.AutoStart || StartupManager.IsEnabled())
                StartupManager.SetEnabled(_settings.Current.AutoStart);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("App", ex, "Windows ile başlatma ayarı uygulanamadı");
        }
    }

    private void ApplySettings()
    {
        if (IsExiting) return;
        ApplyStartupSetting();

        if (_settings.Current.FloatingEnabled)
        {
            _floating ??= new FloatingWindow(_viewModel, _settings);
            _floating.Apply();
            if (!_floating.IsVisible) _floating.Show();
        }
        else
        {
            _floating?.Hide();
        }
    }

    private void ShowMainWindow()
    {
        if (IsExiting) return;
        _mainWindow ??= new MainWindow(_viewModel, _settings, _notifications, OpenDiagnostics);
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenDiagnostics()
    {
        try { DiagnosticsReport.WriteAndOpen(_monitor, _settings); }
        catch (Exception ex)
        {
            DiagnosticLog.Write("App", ex, "Tanılama raporu açılamadı");
            MessageBox.Show($"Tanılama raporu açılamadı:\n{ex.Message}", "V-Battery", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowAbout()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        MessageBox.Show(
            $"V-Battery {v}\n\nBluetooth ve USB cihazların pil seviyesini bildirim alanında gösterir.\n" +
            "Yalnızca cihazın gerçekten bildirdiği değerler gösterilir; tahmini yüzde üretilmez.",
            "V-Battery Hakkında", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            return path != null ? Icon.ExtractAssociatedIcon(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        // Uykudan dönünce Bluetooth'un toparlanması için kısa bekle, sonra yenile.
        _ = Task.Delay(TimeSpan.FromSeconds(8)).ContinueWith(_ => _monitor.RefreshAsync());
    }

    private void ExitApplication()
    {
        if (IsExiting) return;
        IsExiting = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        try { _floating?.Close(); } catch { /* yoksay */ }
        try { _mainWindow?.Close(); } catch { /* yoksay */ }
        try { _tray?.Dispose(); } catch { /* yoksay */ }
        try { _monitor?.Dispose(); } catch { /* yoksay */ }
        try { _settings?.SaveNow(); } catch { /* yoksay */ }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { /* sahibi değilsek */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
