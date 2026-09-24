namespace XboxBatteryMonitor.UI;

using XboxBatteryMonitor.Monitors;

/// <summary>低电量震动提醒：4 个全速脉冲（规格 §4.10），结束后恢复 0。</summary>
public sealed class RumbleNotifier
{
    private readonly XInputMonitor _monitor;

    public RumbleNotifier(XInputMonitor monitor) => _monitor = monitor;

    /// <summary>脉冲序列：4 次（全速 300ms、间隔 200ms）。多次脉冲提高游戏持续刷新震动状态时的命中率。</summary>
    public static IReadOnlyList<(ushort Left, ushort Right, int OnMs, int OffMs)> BuildPattern() =>
        new (ushort, ushort, int, int)[]
        {
            (65535, 65535, 300, 200),
            (65535, 65535, 300, 200),
            (65535, 65535, 300, 200),
            (65535, 65535, 300, 200),
        };

    public async Task RumbleAsync(int slot)
    {
        if (!_monitor.IsAvailable) return;
        foreach ((ushort left, ushort right, int onMs, int offMs) in BuildPattern())
        {
            _monitor.Rumble(slot, left, right);
            await Task.Delay(onMs);
            _monitor.Rumble(slot, 0, 0);
            await Task.Delay(offMs);
        }
    }
}
