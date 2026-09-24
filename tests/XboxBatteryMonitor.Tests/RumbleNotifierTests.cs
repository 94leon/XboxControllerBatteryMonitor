namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.UI;
using Xunit;

public class RumbleNotifierTests
{
    [Fact]
    public void Pattern_IsFourFullPowerPulsesWithStandardTiming()
    {
        var pattern = RumbleNotifier.BuildPattern();
        Assert.Equal(4, pattern.Count);
        Assert.All(pattern, step =>
        {
            Assert.Equal((ushort)65535, step.Left);
            Assert.Equal((ushort)65535, step.Right);
            Assert.Equal(300, step.OnMs);
            Assert.Equal(200, step.OffMs);
        });
    }
}
