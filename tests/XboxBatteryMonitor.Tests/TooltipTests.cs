namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor;
using XboxBatteryMonitor.Core;
using Xunit;

public class TooltipTests
{
    [Fact]
    public void NoControllers_ExplainsWhy()
    {
        Assert.Contains("未发现手柄", AppContext.BuildTooltip(Array.Empty<ControllerStatus>(), bluetoothOn: true, xinputAvailable: true));
        Assert.Contains("蓝牙已关闭", AppContext.BuildTooltip(Array.Empty<ControllerStatus>(), bluetoothOn: false, xinputAvailable: true));
    }

    [Fact]
    public void Controllers_ListedWithBattery()
    {
        var controllers = new[]
        {
            new ControllerStatus("ble:a", "Xbox Wireless Controller", ConnectionKind.Bluetooth, new BatteryValue.Percent(80)),
            new ControllerStatus("xi:1", "适配器手柄 1", ConnectionKind.Adapter, new BatteryValue.Level(XInputBatteryLevel.Low)),
        };
        string tip = AppContext.BuildTooltip(controllers, bluetoothOn: true, xinputAvailable: true);
        Assert.Contains("Xbox Wireless Controller 80%", tip);
        Assert.Contains("适配器手柄 1 40%（估算）", tip);
    }

    [Fact]
    public void LongList_TruncatedTo63Chars()
    {
        var controllers = Enumerable.Range(0, 8)
            .Select(i => new ControllerStatus("id" + i, $"非常长的手柄名称第{i}号", ConnectionKind.Bluetooth, new BatteryValue.Percent(80)))
            .ToList();
        string tip = AppContext.BuildTooltip(controllers, bluetoothOn: true, xinputAvailable: true);
        Assert.True(tip.Length <= 63);   // NotifyIcon.Text 上限
    }
}
