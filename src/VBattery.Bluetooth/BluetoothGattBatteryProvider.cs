using System.Collections.Concurrent;
using VBattery.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace VBattery.Bluetooth;

/// <summary>
/// Eşleşmiş Bluetooth LE cihazlarında standart GATT Battery Service (0x180F) /
/// Battery Level (0x2A19) özelliğini okur. Cihaz bildirim (Notify) destekliyorsa
/// yoklama yapılmaz, değişiklikler olay olarak gelir.
/// </summary>
public sealed class BluetoothGattBatteryProvider : IDeviceProvider
{
    private sealed class Tracked : IDisposable
    {
        public required BluetoothLEDevice Device { get; init; }
        public required string Id { get; init; }
        public GattDeviceService? Service;
        public GattCharacteristic? Characteristic;
        public TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>? ValueHandler;
        public TypedEventHandler<BluetoothLEDevice, object>? ConnectionHandler;
        public readonly SemaphoreSlim Gate = new(1, 1);
        public bool BatteryUnsupported;

        public void ResetGatt()
        {
            if (Characteristic != null && ValueHandler != null)
            {
                try { Characteristic.ValueChanged -= ValueHandler; } catch { /* cihaz gitmiş olabilir */ }
            }
            ValueHandler = null;
            Characteristic = null;
            try { Service?.Dispose(); } catch { /* yoksay */ }
            Service = null;
        }

        public void Dispose()
        {
            if (ConnectionHandler != null)
            {
                try { Device.ConnectionStatusChanged -= ConnectionHandler; } catch { /* yoksay */ }
            }
            ResetGatt();
            try { Device.Dispose(); } catch { /* yoksay */ }
            Gate.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, Tracked> _tracked = new();
    private DeviceWatcher? _watcher;
    private bool _disposed;

    public string Name => "Bluetooth GATT";

    public event EventHandler<DeviceReading>? ReadingChanged;

    public Task StartAsync()
    {
        var selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        _watcher = DeviceInformation.CreateWatcher(selector);
        _watcher.Added += (sender, info) => _ = TrackAsync(info.Id);
        _watcher.Removed += (sender, update) => Untrack(update.Id);
        _watcher.Start();
        DiagnosticLog.Write(Name, "Eşleşmiş Bluetooth LE cihaz izleyicisi başlatıldı.");
        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        foreach (var t in _tracked.Values.ToList())
        {
            await ReadBatteryAsync(t).ConfigureAwait(false);
        }
    }

    private async Task TrackAsync(string deviceId)
    {
        if (_disposed || _tracked.ContainsKey(deviceId)) return;
        try
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device == null) return;

            var tracked = new Tracked { Device = device, Id = $"ble:{device.BluetoothAddress:X12}" };
            if (!_tracked.TryAdd(deviceId, tracked))
            {
                tracked.Dispose();
                return;
            }

            tracked.ConnectionHandler = (_, _) => _ = ReadBatteryAsync(tracked);
            device.ConnectionStatusChanged += tracked.ConnectionHandler;
            await ReadBatteryAsync(tracked).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(Name, ex, $"Cihaz izlenemedi ({deviceId})");
        }
    }

    private void Untrack(string deviceId)
    {
        if (!_tracked.TryRemove(deviceId, out var t)) return;
        Emit(t, connected: false, percent: null, supported: !t.BatteryUnsupported);
        t.Dispose();
    }

    private async Task ReadBatteryAsync(Tracked t)
    {
        if (_disposed) return;
        if (!await t.Gate.WaitAsync(0).ConfigureAwait(false)) return; // aynı cihaz için eşzamanlı okuma yok
        try
        {
            if (t.Device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                t.ResetGatt(); // yeniden bağlanınca servis yeniden alınır
                Emit(t, connected: false, percent: null, supported: !t.BatteryUnsupported);
                return;
            }

            if (t.Characteristic == null && !await DiscoverAsync(t).ConfigureAwait(false)) return;

            var read = await t.Characteristic!.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status == GattCommunicationStatus.Success)
            {
                Emit(t, connected: true, percent: ParseLevel(read.Value), supported: true);
            }
            else
            {
                DiagnosticLog.Write(Name, $"{t.Device.Name}: pil okunamadı ({read.Status}).");
                Emit(t, connected: true, percent: null, supported: true);
            }
        }
        catch (Exception ex)
        {
            // Hatalı cihaz yalnızca kendi durumunu bozar.
            t.ResetGatt();
            DiagnosticLog.Write(Name, ex, $"{t.Device.Name}: okuma hatası");
            Emit(t, connected: t.Device.ConnectionStatus == BluetoothConnectionStatus.Connected, percent: null, supported: !t.BatteryUnsupported);
        }
        finally
        {
            t.Gate.Release();
        }
    }

    /// <summary>Battery Service + Battery Level özelliğini bulur; Notify varsa abone olur.</summary>
    private async Task<bool> DiscoverAsync(Tracked t)
    {
        var services = await t.Device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
        if (services.Status != GattCommunicationStatus.Success)
        {
            DiagnosticLog.Write(Name, $"{t.Device.Name}: GATT servis sorgusu başarısız ({services.Status}).");
            Emit(t, connected: true, percent: null, supported: true);
            return false;
        }

        if (services.Services.Count == 0)
        {
            t.BatteryUnsupported = true;
            Emit(t, connected: true, percent: null, supported: false);
            return false;
        }

        t.Service = services.Services[0];
        for (var i = 1; i < services.Services.Count; i++) services.Services[i].Dispose();

        var chars = await t.Service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
        if (chars.Status != GattCommunicationStatus.Success || chars.Characteristics.Count == 0)
        {
            t.ResetGatt();
            t.BatteryUnsupported = chars.Status == GattCommunicationStatus.Success;
            Emit(t, connected: true, percent: null, supported: !t.BatteryUnsupported);
            return false;
        }

        t.BatteryUnsupported = false;
        t.Characteristic = chars.Characteristics[0];

        if (t.Characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
        {
            var cccd = await t.Characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (cccd == GattCommunicationStatus.Success)
            {
                t.ValueHandler = (_, args) =>
                {
                    try { Emit(t, connected: true, percent: ParseLevel(args.CharacteristicValue), supported: true); }
                    catch (Exception ex) { DiagnosticLog.Write(Name, ex, "Bildirim işlenemedi"); }
                };
                t.Characteristic.ValueChanged += t.ValueHandler;
            }
        }

        return true;
    }

    private static int? ParseLevel(IBuffer buffer)
    {
        if (buffer.Length < 1) return null;
        var level = DataReader.FromBuffer(buffer).ReadByte();
        return level <= 100 ? level : null; // Battery Level 0-100 dışında geçerli değildir
    }

    private void Emit(Tracked t, bool connected, int? percent, bool supported)
    {
        if (_disposed) return;
        var name = t.Device.Name;
        ReadingChanged?.Invoke(this, new DeviceReading(
            t.Id,
            name,
            ResolveType(t.Device, name),
            ConnectionType.Bluetooth,
            connected,
            connected ? percent : null,
            ChargeState.Unknown, // GATT Battery Level şarj durumunu taşımaz
            supported));
    }

    private static DeviceType ResolveType(BluetoothLEDevice device, string name)
    {
        try
        {
            var appearance = device.Appearance;
            if (appearance != null && appearance.Category == BluetoothLEAppearanceCategories.HumanInterfaceDevice)
            {
                var sub = appearance.SubCategory;
                if (sub == BluetoothLEAppearanceSubcategories.Keyboard) return DeviceType.Keyboard;
                if (sub == BluetoothLEAppearanceSubcategories.Mouse) return DeviceType.Mouse;
                if (sub == BluetoothLEAppearanceSubcategories.Gamepad || sub == BluetoothLEAppearanceSubcategories.Joystick)
                    return DeviceType.Gamepad;
            }
        }
        catch { /* görünüm bilgisi yoksa isimden tahmin et */ }

        var guess = DeviceTypeGuesser.FromName(name);
        return guess == DeviceType.Unknown ? DeviceType.Other : guess;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_watcher != null &&
                (_watcher.Status == DeviceWatcherStatus.Started || _watcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                _watcher.Stop();
            }
        }
        catch { /* yoksay */ }

        foreach (var t in _tracked.Values) t.Dispose();
        _tracked.Clear();
    }
}
