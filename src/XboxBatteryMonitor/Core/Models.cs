namespace XboxBatteryMonitor.Core;

/// <summary>XInput 上报的电池类型（xinput.h 的 BATTERY_TYPE）。</summary>
public enum XInputBatteryType : byte
{
    Disconnected = 0, // 已检测到手柄但尚未上报电量（也见于蓝牙手柄的错报）
    Wired = 1,        // USB 有线供电，无电池数据
    Alkaline = 2,     // 碱性电池（AA）
    Nimh = 3,         // 镍氢充电电池组
    Unknown = 255,
}

/// <summary>XInput 上报的电池档位（xinput.h 的 BATTERY_LEVEL），仅 4 档。</summary>
public enum XInputBatteryLevel : byte
{
    Empty = 0,
    Low = 1,
    Medium = 2,
    Full = 3,
}

/// <summary>手柄与主机的连接方式。</summary>
public enum ConnectionKind
{
    Bluetooth,
    Adapter,
    Usb,
}

/// <summary>电量值：精确百分比（BLE）/ 4 档（XInput）/ 有线 / 等待读数。</summary>
public abstract record BatteryValue
{
    public sealed record Percent(int Value) : BatteryValue;
    public sealed record Level(XInputBatteryLevel Value) : BatteryValue;
    public sealed record Wired() : BatteryValue;
    public sealed record Waiting() : BatteryValue;

    /// <summary>折算为估算百分比；有线/等待返回 null。XInput 4 档按社区惯例映射 10/40/70/100。</summary>
    public int? ToEstimatedPercent() => this switch
    {
        Percent p => Math.Clamp(p.Value, 0, 100),
        Level l => l.Value switch
        {
            XInputBatteryLevel.Empty => 10,
            XInputBatteryLevel.Low => 40,
            XInputBatteryLevel.Medium => 70,
            _ => 100,
        },
        _ => null,
    };
}

/// <summary>聚合后的单个手柄状态。</summary>
public sealed record ControllerStatus(
    string Id,
    string DisplayName,
    ConnectionKind Kind,
    BatteryValue Battery);

/// <summary>应用设置。约束：0 &lt; 危险阈值 &lt; 警告阈值 &lt; 100。</summary>
public sealed record AppSettings(
    int WarningThreshold = 20,
    int CriticalThreshold = 10,
    bool RumbleEnabled = true)
{
    public bool IsValid =>
        CriticalThreshold > 0 && CriticalThreshold < WarningThreshold && WarningThreshold < 100;

    /// <summary>非法配置回退为默认值。</summary>
    public static AppSettings Sanitized(AppSettings settings) => settings.IsValid ? settings : new AppSettings();
}

/// <summary>告警档位。数值大小即严重程度（排序用）。</summary>
public enum AlertTier
{
    Warning = 0,
    Critical = 1,
}

/// <summary>一次阈值跨越事件。</summary>
public sealed record BatteryAlert(
    string ControllerId,
    string DisplayName,
    AlertTier Tier,
    int Percent);
