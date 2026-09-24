namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.Core;
using Xunit;

public class ModelsTests
{
    [Theory]
    [InlineData(XInputBatteryLevel.Empty, 10)]
    [InlineData(XInputBatteryLevel.Low, 40)]
    [InlineData(XInputBatteryLevel.Medium, 70)]
    [InlineData(XInputBatteryLevel.Full, 100)]
    public void Level_EstimatesPercent(XInputBatteryLevel level, int expected)
    {
        var value = new BatteryValue.Level(level);
        Assert.Equal(expected, value.ToEstimatedPercent());
    }

    [Fact]
    public void Percent_PassesThroughAndClamps()
    {
        Assert.Equal(80, new BatteryValue.Percent(80).ToEstimatedPercent());
        Assert.Equal(100, new BatteryValue.Percent(150).ToEstimatedPercent());
        Assert.Equal(0, new BatteryValue.Percent(-5).ToEstimatedPercent());
    }

    [Fact]
    public void WiredAndWaiting_HaveNoPercent()
    {
        Assert.Null(new BatteryValue.Wired().ToEstimatedPercent());
        Assert.Null(new BatteryValue.Waiting().ToEstimatedPercent());
    }

    [Fact]
    public void SettingsValidity()
    {
        Assert.True(new AppSettings(20, 10, true).IsValid);
        Assert.False(new AppSettings(10, 10, true).IsValid);      // 危险 == 警告
        Assert.False(new AppSettings(20, 0, true).IsValid);       // 危险 = 0
        Assert.False(new AppSettings(100, 10, true).IsValid);     // 警告 = 100
        Assert.Equal(new AppSettings(), AppSettings.Sanitized(new AppSettings(5, 50, true)));
        Assert.Equal(new AppSettings(30, 15, false), AppSettings.Sanitized(new AppSettings(30, 15, false)));
    }
}
