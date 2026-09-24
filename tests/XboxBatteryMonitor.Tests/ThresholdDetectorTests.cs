namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.Core;
using Xunit;

public class ThresholdDetectorTests
{
    private static readonly AppSettings Settings = new(WarningThreshold: 20, CriticalThreshold: 10);

    private static ControllerStatus Ctrl(int percent) =>
        new("c1", "手柄", ConnectionKind.Bluetooth, new BatteryValue.Percent(percent));

    [Fact]
    public void FreshController_BelowWarning_AlertsImmediately()
    {
        var d = new ThresholdDetector(Settings);
        // 应用启动/手柄刚出现时视为从 100 开始：低电应立即提醒
        var alerts = d.Process(Ctrl(15));
        Assert.Single(alerts, a => a.Tier == AlertTier.Warning);
    }

    [Fact]
    public void CrossingWarning_AlertsOnce()
    {
        var d = new ThresholdDetector(Settings);
        Assert.Empty(d.Process(Ctrl(25)));
        Assert.Single(d.Process(Ctrl(18)), a => a.Tier == AlertTier.Warning);
        Assert.Empty(d.Process(Ctrl(17)));   // 仍在低位，不重复
        Assert.Empty(d.Process(Ctrl(12)));
    }

    [Fact]
    public void CrossingCritical_AlertsCritical()
    {
        var d = new ThresholdDetector(Settings);
        d.Process(Ctrl(25));
        d.Process(Ctrl(18));
        Assert.Single(d.Process(Ctrl(8)), a => a.Tier == AlertTier.Critical);
    }

    [Fact]
    public void BigDrop_FiresBothTiers()
    {
        var d = new ThresholdDetector(Settings);
        var alerts = d.Process(Ctrl(5));     // 100 直接跌到 5
        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public void MostSevere_DedupesPerController()
    {
        var d = new ThresholdDetector(Settings);
        var alerts = d.Process(Ctrl(5));
        var severest = ThresholdDetector.MostSeverePerController(alerts);
        Assert.Single(severest, a => a.Tier == AlertTier.Critical);
    }

    [Fact]
    public void Recovering_Rearms()
    {
        var d = new ThresholdDetector(Settings);
        d.Process(Ctrl(18));
        d.Process(Ctrl(25));                 // 回升
        Assert.Single(d.Process(Ctrl(19)));  // 再次跌落 → 重新提醒
    }

    [Fact]
    public void WaitingAndWired_NeverAlert_AndKeepLastValue()
    {
        var d = new ThresholdDetector(Settings);
        d.Process(Ctrl(25));
        Assert.Empty(d.Process(new ControllerStatus("c1", "手柄", ConnectionKind.Bluetooth, new BatteryValue.Waiting())));
        Assert.Empty(d.Process(new ControllerStatus("c1", "手柄", ConnectionKind.Usb, new BatteryValue.Wired())));
        // 等待期间保留旧值：25 → 等待 → 18 仍视为从 25 跨越
        Assert.Single(d.Process(Ctrl(18)), a => a.Tier == AlertTier.Warning);
    }

    [Fact]
    public void Reset_RearmsWithoutRecovery()
    {
        var d = new ThresholdDetector(Settings);
        d.Process(Ctrl(18));                 // 已在低位
        d.Reset("c1");                       // 手柄消失再出现（重连）
        Assert.Single(d.Process(Ctrl(17)), a => a.Tier == AlertTier.Warning);
    }

    [Fact]
    public void LevelValue_UsesEstimatedPercent()
    {
        var d = new ThresholdDetector(Settings);
        var low = new ControllerStatus("x", "适配器", ConnectionKind.Adapter,
            new BatteryValue.Level(XInputBatteryLevel.Low));   // 估算 40%
        Assert.Empty(d.Process(low));
        var empty = low with { Battery = new BatteryValue.Level(XInputBatteryLevel.Empty) }; // 10%
        var alerts = d.Process(empty);
        Assert.Contains(alerts, a => a.Tier == AlertTier.Warning);
    }

    [Fact]
    public void UpdateSettings_ChangesThresholdsLive()
    {
        var d = new ThresholdDetector(Settings);
        d.Process(Ctrl(25));
        d.UpdateSettings(new AppSettings(WarningThreshold: 30, CriticalThreshold: 12));
        // last=25，25>30 为假 → 调整阈值本身不产生虚假跨越
        Assert.Empty(d.Process(Ctrl(26)));
        // 回升到 35 后再跌落 → 按新阈值 30 触发
        d.Process(Ctrl(35));
        Assert.Single(d.Process(Ctrl(28)), a => a.Tier == AlertTier.Warning);
    }
}
