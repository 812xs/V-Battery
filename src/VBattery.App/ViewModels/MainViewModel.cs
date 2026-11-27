using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using VBattery.DeviceDetection;
using VBattery.Settings;

namespace VBattery.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DeviceMonitor _monitor;
    private readonly SettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, DeviceViewModel> _byId = new();

    public MainViewModel(DeviceMonitor monitor, SettingsService settings, Dispatcher dispatcher)
    {
        _monitor = monitor;
        _settings = settings;
        _dispatcher = dispatcher;

        _monitor.DeviceListChanged += (_, _) => _dispatcher.BeginInvoke(new Action(Rebuild));
        _monitor.DeviceUpdated += (_, _) => _dispatcher.BeginInvoke(new Action(UpdateCounts));
        _settings.Changed += (_, _) => _dispatcher.BeginInvoke(new Action(RefreshAll));
        Rebuild();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    public int ConnectedCount => Devices.Count(d => d.IsConnected);
    public bool NoneConnected => ConnectedCount == 0;
    public bool NoDevices => Devices.Count == 0;

    private void Rebuild()
    {
        foreach (var d in _monitor.Devices)
        {
            if (_byId.ContainsKey(d.Id)) continue;
            var vm = new DeviceViewModel(d, _settings);
            _byId[d.Id] = vm;
            Devices.Add(vm);
        }
        UpdateCounts();
    }

    private void RefreshAll()
    {
        foreach (var d in Devices) d.Refresh();
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoneConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoDevices)));
    }
}
