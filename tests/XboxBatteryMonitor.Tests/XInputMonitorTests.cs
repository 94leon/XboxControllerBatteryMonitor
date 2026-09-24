namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.Core;
using XboxBatteryMonitor.Monitors;
using Xunit;

public class XInputMonitorTests
{
    internal sealed class FakeXInputApi : IXInputApi
    {
        public bool IsAvailable { get; set; } = true;
        public Dictionary<int, bool> Connected { get; } = new();
        public Dictionary<int, XInputBatteryInfo> Battery { get; } = new();
        public List<(int Slot, ushort Left, ushort Right)> Vibrations { get; } = new();

        public bool IsConnected(int userIndex) => Connected.TryGetValue(userIndex, out bool c) && c;

        public XInputBatteryInfo GetBatteryInformation(int userIndex) =>
            Battery.TryGetValue(userIndex, out XInputBatteryInfo b)
                ? b
                : new XInputBatteryInfo(XInputBatteryType.Disconnected, XInputBatteryLevel.Empty);

        public void SetVibration(int userIndex, ushort left, ushort right) =>
            Vibrations.Add((userIndex, left, right));

        public void Dispose() { }
    }

    [Fact]
    public void PollOnce_CollectsConnectedSlots()
    {
        var api = new FakeXInputApi();
        api.Connected[0] = true;
        api.Battery[0] = new XInputBatteryInfo(XInputBatteryType.Alkaline, XInputBatteryLevel.Low);
        api.Connected[2] = true;
        api.Battery[2] = new XInputBatteryInfo(XInputBatteryType.Wired, XInputBatteryLevel.Empty);

        using var monitor = new XInputMonitor(api, startPolling: false);
        int events = 0;
        monitor.SnapshotChanged += () => events++;
        monitor.PollOnce();

        Assert.Equal(1, events);
        Assert.Equal(2, monitor.CurrentSnapshot.Count);
        Assert.Equal(new XInputSlotSnapshot(0, true, XInputBatteryType.Alkaline, XInputBatteryLevel.Low), monitor.CurrentSnapshot[0]);
        Assert.Equal(new XInputSlotSnapshot(2, true, XInputBatteryType.Wired, XInputBatteryLevel.Empty), monitor.CurrentSnapshot[1]);
    }

    [Fact]
    public void PollOnce_NoControllers_EmptySnapshot()
    {
        using var monitor = new XInputMonitor(new FakeXInputApi(), startPolling: false);
        monitor.PollOnce();
        Assert.Empty(monitor.CurrentSnapshot);
    }

    [Fact]
    public void ApiUnavailable_EmptySnapshotAndNoCalls()
    {
        var api = new FakeXInputApi { IsAvailable = false };
        using var monitor = new XInputMonitor(api, startPolling: false);
        monitor.PollOnce();
        Assert.Empty(monitor.CurrentSnapshot);
        Assert.Empty(api.Connected);   // 未触碰 API
    }

    [Fact]
    public void DisappearingController_LeavesSnapshot()
    {
        var api = new FakeXInputApi();
        api.Connected[1] = true;
        api.Battery[1] = new XInputBatteryInfo(XInputBatteryType.Nimh, XInputBatteryLevel.Full);
        using var monitor = new XInputMonitor(api, startPolling: false);
        monitor.PollOnce();
        Assert.Single(monitor.CurrentSnapshot);

        api.Connected[1] = false;
        monitor.PollOnce();
        Assert.Empty(monitor.CurrentSnapshot);
    }

    [Fact]
    public void Rumble_ForwardsToApi()
    {
        var api = new FakeXInputApi();
        using var monitor = new XInputMonitor(api, startPolling: false);
        monitor.Rumble(3, 65535, 0);
        Assert.Contains((3, (ushort)65535, (ushort)0), api.Vibrations);
    }
}
