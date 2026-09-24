namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.Core;
using Xunit;

public class AggregatorTests
{
    private static readonly XInputSlotSnapshot[] NoXInput = Array.Empty<XInputSlotSnapshot>();
    private static readonly BleDeviceSnapshot[] NoBle = Array.Empty<BleDeviceSnapshot>();

    private static XInputSlotSnapshot Slot(int n, XInputBatteryType type, XInputBatteryLevel level = XInputBatteryLevel.Full) =>
        new(n, true, type, level);

    [Fact]
    public void EmptyInputs_ProduceEmptyOutput()
    {
        Assert.Empty(Aggregator.Aggregate(NoXInput, NoBle));
    }

    [Fact]
    public void BleOnly_ConnectedGamepadWithPercent()
    {
        var ble = new[] { new BleDeviceSnapshot("id1", "Xbox Wireless Controller", true, 80) };
        var result = Aggregator.Aggregate(NoXInput, ble);
        var c = Assert.Single(result);
        Assert.Equal(ConnectionKind.Bluetooth, c.Kind);
        Assert.Equal(new BatteryValue.Percent(80), c.Battery);
        Assert.Equal("Xbox Wireless Controller", c.DisplayName);
        Assert.Equal("ble:id1", c.Id);
    }

    [Fact]
    public void BleOnly_DisconnectedOrWaitingHandled()
    {
        var ble = new[]
        {
            new BleDeviceSnapshot("id1", "手柄A", false, null),   // 未连接 → 排除
            new BleDeviceSnapshot("id2", "手柄B", true, null),    // 已连接但还没读到 → Waiting
        };
        var result = Aggregator.Aggregate(NoXInput, ble);
        var c = Assert.Single(result);
        Assert.Equal(new BatteryValue.Waiting(), c.Battery);
    }

    [Fact]
    public void XInputOnly_AdapterAndUsb()
    {
        var xinput = new[]
        {
            Slot(0, XInputBatteryType.Alkaline, XInputBatteryLevel.Low),
            Slot(1, XInputBatteryType.Wired),
        };
        var result = Aggregator.Aggregate(xinput, NoBle);
        Assert.Equal(2, result.Count);
        Assert.Equal(ConnectionKind.Adapter, result[0].Kind);
        Assert.Equal("适配器手柄 1", result[0].DisplayName);
        Assert.Equal(new BatteryValue.Level(XInputBatteryLevel.Low), result[0].Battery);
        Assert.Equal(ConnectionKind.Usb, result[1].Kind);
        Assert.Equal("USB 手柄 2", result[1].DisplayName);
    }

    [Fact]
    public void Mixed_OneBlePlusTwoWirelessSlots_KeepsOneAdapter()
    {
        // 蓝牙 1 台占用槽位 0（表现为 Disconnected 错报），适配器手柄在槽位 1
        var xinput = new[]
        {
            Slot(0, XInputBatteryType.Disconnected),
            Slot(1, XInputBatteryType.Alkaline, XInputBatteryLevel.Medium),
        };
        var ble = new[] { new BleDeviceSnapshot("id1", "Xbox Wireless Controller", true, 66) };
        var result = Aggregator.Aggregate(xinput, ble);
        Assert.Equal(2, result.Count);
        Assert.Equal(ConnectionKind.Bluetooth, result[0].Kind);              // BLE 优先展示
        Assert.Equal(ConnectionKind.Adapter, result[1].Kind);
        Assert.Equal("xi:1", result[1].Id);                                  // 选中真实读数的槽位 1
        Assert.Equal(new BatteryValue.Level(XInputBatteryLevel.Medium), result[1].Battery);
    }

    [Fact]
    public void Mixed_AdapterCountClampedToZero()
    {
        // 蓝牙 2 台 + XInput 无线槽位 2 → 无适配器手柄
        var xinput = new[] { Slot(0, XInputBatteryType.Alkaline), Slot(1, XInputBatteryType.Alkaline) };
        var ble = new[]
        {
            new BleDeviceSnapshot("a", "手柄", true, 50),
            new BleDeviceSnapshot("b", "手柄", true, 60),
        };
        var result = Aggregator.Aggregate(xinput, ble);
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(ConnectionKind.Bluetooth, c.Kind));
    }

    [Fact]
    public void Mixed_OnlyDisconnectedSlots_FallsBack()
    {
        // 蓝牙 1 台 + 两个 Disconnected 槽位 → 适配器手柄取槽位号最小的 Disconnected
        var xinput = new[] { Slot(0, XInputBatteryType.Disconnected), Slot(3, XInputBatteryType.Disconnected) };
        var ble = new[] { new BleDeviceSnapshot("a", "手柄", true, 50) };
        var result = Aggregator.Aggregate(xinput, ble);
        Assert.Equal(2, result.Count);
        Assert.Equal("xi:0", result[1].Id);
        Assert.Equal(new BatteryValue.Waiting(), result[1].Battery);
    }

    [Fact]
    public void DuplicateBleNames_GetNumberSuffix()
    {
        var ble = new[]
        {
            new BleDeviceSnapshot("a", "手柄", true, 50),
            new BleDeviceSnapshot("b", "手柄", true, 60),
        };
        var result = Aggregator.Aggregate(NoXInput, ble);
        Assert.Equal("手柄", result[0].DisplayName);
        Assert.Equal("手柄 #2", result[1].DisplayName);
    }
}
