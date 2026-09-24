namespace XboxBatteryMonitor.Core;

/// <summary>
/// 阈值跨越检测状态机。语义：
/// - 每手柄记录上次估算百分比；新出现（启动/重连）视为 100；
/// - 从 &gt;阈值 跌到 ≤阈值 的那一刻各档触发一次；回升后自然重新武装；
/// - Waiting/Wired 不评估也不更新上次值；手柄消失由调用方 Reset。
/// </summary>
public sealed class ThresholdDetector
{
    private readonly Dictionary<string, int> _lastPercent = new();
    private int _warning;
    private int _critical;

    public ThresholdDetector(AppSettings settings)
    {
        _warning = settings.WarningThreshold;
        _critical = settings.CriticalThreshold;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _warning = settings.WarningThreshold;
        _critical = settings.CriticalThreshold;
    }

    public IReadOnlyList<BatteryAlert> Process(ControllerStatus controller)
    {
        int? percent = controller.Battery.ToEstimatedPercent();
        if (percent is null)
            return Array.Empty<BatteryAlert>();   // 等待读数/有线：不评估、不更新状态

        int current = percent.Value;
        int last = _lastPercent.TryGetValue(controller.Id, out int stored) ? stored : 100;
        _lastPercent[controller.Id] = current;

        var alerts = new List<BatteryAlert>(2);
        if (last > _warning && current <= _warning)
            alerts.Add(new BatteryAlert(controller.Id, controller.DisplayName, AlertTier.Warning, current));
        if (last > _critical && current <= _critical)
            alerts.Add(new BatteryAlert(controller.Id, controller.DisplayName, AlertTier.Critical, current));
        return alerts;
    }

    /// <summary>手柄消失时清除其状态，重连后按新会话处理（低电立即再提醒）。</summary>
    public void Reset(string controllerId) => _lastPercent.Remove(controllerId);

    /// <summary>同一手柄同时跨越两档时只保留最严重一档（展示用）。</summary>
    public static IReadOnlyList<BatteryAlert> MostSeverePerController(IEnumerable<BatteryAlert> alerts) =>
        alerts
            .GroupBy(a => a.ControllerId)
            .Select(g => g.OrderByDescending(a => (int)a.Tier).First())
            .ToList();
}
