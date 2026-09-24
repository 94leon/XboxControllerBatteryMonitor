namespace XboxBatteryMonitor.Monitors;

using XboxBatteryMonitor.Core;

/// <summary>
/// 每 2 秒轮询 4 个 XInput 槽位。快照事件在轮询线程（后台线程）上触发，
/// 订阅方负责封送回 UI 线程。
/// </summary>
public sealed class XInputMonitor : IDisposable
{
    public const int SlotCount = 4;
    private const int PollIntervalMs = 2000;

    private readonly IXInputApi _api;
    private readonly System.Threading.Timer? _timer;

    public event Action? SnapshotChanged;

    public bool IsAvailable => _api.IsAvailable;

    public IReadOnlyList<XInputSlotSnapshot> CurrentSnapshot { get; private set; } = Array.Empty<XInputSlotSnapshot>();

    public XInputMonitor(IXInputApi api, bool startPolling = true)
    {
        _api = api;
        if (startPolling)
            _timer = new System.Threading.Timer(_ => PollOnce(), null, PollIntervalMs, PollIntervalMs);
    }

    /// <summary>立即轮询一次并更新快照。</summary>
    public void PollOnce()
    {
        if (!IsAvailable)
        {
            SetSnapshot(Array.Empty<XInputSlotSnapshot>());
            return;
        }

        var snapshot = new List<XInputSlotSnapshot>(SlotCount);
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (!_api.IsConnected(slot)) continue;
            XInputBatteryInfo info = _api.GetBatteryInformation(slot);
            snapshot.Add(new XInputSlotSnapshot(slot, true, info.Type, info.Level));
        }
        SetSnapshot(snapshot);
    }

    public void Rumble(int slot, ushort left, ushort right)
    {
        if (slot >= 0 && slot < SlotCount)
            _api.SetVibration(slot, left, right);
    }

    private void SetSnapshot(IReadOnlyList<XInputSlotSnapshot> snapshot)
    {
        CurrentSnapshot = snapshot;
        SnapshotChanged?.Invoke();
    }

    public void Dispose() => _timer?.Dispose();
}
