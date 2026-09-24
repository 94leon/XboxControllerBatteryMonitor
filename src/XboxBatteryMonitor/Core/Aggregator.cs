namespace XboxBatteryMonitor.Core;

/// <summary>XInput 单个槽位的轮询快照。</summary>
public sealed record XInputSlotSnapshot(
    int Slot,
    bool IsConnected,
    XInputBatteryType BatteryType,
    XInputBatteryLevel BatteryLevel);

/// <summary>BLE 单个已配对手柄的快照。Percent 为 null 表示已连接但尚未读到电量。</summary>
public sealed record BleDeviceSnapshot(
    string DeviceId,
    string Name,
    bool IsConnected,
    int? Percent);

/// <summary>把两个监控通道的快照合并为统一的手柄列表（规格 §6 去重规则）。</summary>
public static class Aggregator
{
    public static IReadOnlyList<ControllerStatus> Aggregate(
        IReadOnlyList<XInputSlotSnapshot> xinput,
        IReadOnlyList<BleDeviceSnapshot> ble)
    {
        var result = new List<ControllerStatus>();

        // 1) 蓝牙手柄：BLE 精确百分比（XInput 对蓝牙手柄的电量是错值，一律以 BLE 为准）
        List<BleDeviceSnapshot> connectedBle = ble.Where(b => b.IsConnected).ToList();
        Dictionary<string, string> displayNames = AssignDisplayNames(connectedBle);
        foreach (BleDeviceSnapshot b in connectedBle)
        {
            BatteryValue battery = b.Percent is int p ? new BatteryValue.Percent(p) : new BatteryValue.Waiting();
            result.Add(new ControllerStatus("ble:" + b.DeviceId, displayNames[b.DeviceId], ConnectionKind.Bluetooth, battery));
        }

        // 2) 适配器手柄：4 档读数，与蓝牙数量去重（D = max(0, K − N)）
        List<XInputSlotSnapshot> wireless = xinput
            .Where(s => s.IsConnected && s.BatteryType is XInputBatteryType.Alkaline or XInputBatteryType.Nimh or XInputBatteryType.Disconnected)
            .ToList();
        int adapterCount = Math.Max(0, wireless.Count - connectedBle.Count);
        List<XInputSlotSnapshot> candidates = wireless
            .Where(s => s.BatteryType is XInputBatteryType.Alkaline or XInputBatteryType.Nimh) // 有真实读数的优先
            .OrderBy(s => s.Slot)
            .ToList();
        if (candidates.Count < adapterCount)
            candidates.AddRange(wireless
                .Where(s => s.BatteryType == XInputBatteryType.Disconnected)
                .OrderBy(s => s.Slot));
        for (int i = 0; i < Math.Min(adapterCount, candidates.Count); i++)
        {
            XInputSlotSnapshot s = candidates[i];
            BatteryValue battery = s.BatteryType == XInputBatteryType.Disconnected
                ? new BatteryValue.Waiting()
                : new BatteryValue.Level(s.BatteryLevel);
            result.Add(new ControllerStatus("xi:" + s.Slot, $"适配器手柄 {i + 1}", ConnectionKind.Adapter, battery));
        }

        // 3) USB 有线：显示"有线"，不参与低电量提醒
        foreach (XInputSlotSnapshot s in xinput
            .Where(s => s.IsConnected && s.BatteryType == XInputBatteryType.Wired)
            .OrderBy(s => s.Slot))
        {
            result.Add(new ControllerStatus("xi:" + s.Slot, $"USB 手柄 {s.Slot + 1}", ConnectionKind.Usb, new BatteryValue.Wired()));
        }

        return result;
    }

    /// <summary>同名蓝牙设备加 " #2"/" #3" 序号区分。</summary>
    internal static Dictionary<string, string> AssignDisplayNames(IReadOnlyList<BleDeviceSnapshot> devices)
    {
        var names = new Dictionary<string, string>();
        foreach (var group in devices.GroupBy(d => d.Name))
        {
            int i = 1;
            foreach (BleDeviceSnapshot d in group)
            {
                names[d.DeviceId] = i == 1 ? d.Name : $"{d.Name} #{i}";
                i++;
            }
        }
        return names;
    }
}
