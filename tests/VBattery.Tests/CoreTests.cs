using VBattery.Core;
using VBattery.DeviceDetection;
using VBattery.HID;
using Xunit;

namespace VBattery.Tests;

public class HidBatteryMathTests
{
    [Theory]
    [InlineData(0u, 0, 100, 0)]
    [InlineData(50u, 0, 100, 50)]
    [InlineData(100u, 0, 100, 100)]
    [InlineData(128u, 0, 255, 50)]
    [InlineData(255u, 0, 255, 100)]
    public void Converts_logical_range_to_percent(uint value, int min, int max, int expected) =>
        Assert.Equal(expected, HidBatteryMath.ToPercent(value, min, max));

    [Theory]
    [InlineData(101u, 0, 100)]
    [InlineData(5u, 10, 100)]
    [InlineData(10u, 100, 100)]
    [InlineData(10u, 100, 0)]
    public void Rejects_invalid_values_instead_of_guessing(uint value, int min, int max) =>
        Assert.Null(HidBatteryMath.ToPercent(value, min, max));
}

public class DeviceTypeGuesserTests
{
    [Theory]
    [InlineData("Logitech MX Master 3 Mouse", DeviceType.Mouse)]
    [InlineData("Keychron K2 Keyboard", DeviceType.Keyboard)]
    [InlineData("WH-1000XM4 Headphones", DeviceType.Headset)]
    [InlineData("Xbox Wireless Controller", DeviceType.Gamepad)]
    [InlineData("Bilinmeyen Cihaz 42", DeviceType.Unknown)]
    [InlineData(null, DeviceType.Unknown)]
    public void Guesses_type_from_name(string? name, DeviceType expected) =>
        Assert.Equal(expected, DeviceTypeGuesser.FromName(name));
}

public class DeviceInfoTests
{
    [Fact]
    public void Shows_dashes_when_battery_unavailable()
    {
        var d = new DeviceInfo("a");
        d.Apply(new DeviceReading("a", "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, true, null, BatterySupported: false));
        Assert.Equal("--", d.BatteryText);
        Assert.False(d.HasBatteryData);
    }

    [Fact]
    public void Shows_percent_and_reports_change_once()
    {
        var d = new DeviceInfo("a");
        var r = new DeviceReading("a", "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, true, 14);
        Assert.True(d.Apply(r));
        Assert.Equal("14%", d.BatteryText);
        Assert.False(d.Apply(r));
    }

    [Fact]
    public void Disconnected_device_shows_dashes()
    {
        var d = new DeviceInfo("a");
        d.Apply(new DeviceReading("a", "Mouse", DeviceType.Mouse, ConnectionType.Bluetooth, false, 80));
        Assert.Equal("--", d.BatteryText);
    }

    [Fact]
    public void Coarse_band_is_shown_without_inventing_a_percent()
    {
        var d = new DeviceInfo("pad");
        d.Apply(new DeviceReading("pad", "Pad", DeviceType.Gamepad, ConnectionType.Wireless, true, Band: BatteryBand.Medium));
        Assert.Equal("Orta", d.BatteryText);
        Assert.Null(d.BatteryPercent);
    }
}

public class DeviceMonitorTests
{
    private sealed class FakeProvider : IDeviceProvider
    {
        public string Name => "fake";
        public event EventHandler<DeviceReading>? ReadingChanged;
        public Task StartAsync() => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
        public void Emit(DeviceReading r) => ReadingChanged?.Invoke(this, r);
        public void Dispose() { }
    }

    [Fact]
    public void Same_id_is_listed_once_and_updated()
    {
        var provider = new FakeProvider();
        using var monitor = new DeviceMonitor();
        monitor.AddProvider(provider);
        var listChanged = 0;
        monitor.DeviceListChanged += (_, _) => listChanged++;

        provider.Emit(new DeviceReading("x", "Mouse", DeviceType.Mouse, ConnectionType.Usb, true, 50));
        provider.Emit(new DeviceReading("x", "Mouse", DeviceType.Mouse, ConnectionType.Usb, true, 49));

        Assert.Single(monitor.Devices);
        Assert.Equal(49, monitor.Devices[0].BatteryPercent);
        Assert.Equal(1, listChanged);
    }

    [Fact]
    public void Faulty_listener_does_not_break_monitor()
    {
        var provider = new FakeProvider();
        using var monitor = new DeviceMonitor();
        monitor.AddProvider(provider);
        monitor.DeviceUpdated += (_, _) => throw new InvalidOperationException("boom");

        provider.Emit(new DeviceReading("x", "Mouse", DeviceType.Mouse, ConnectionType.Usb, true, 50));

        Assert.Single(monitor.Devices);
    }
}
