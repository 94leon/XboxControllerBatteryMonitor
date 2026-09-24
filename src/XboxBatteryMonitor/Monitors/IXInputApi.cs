namespace XboxBatteryMonitor.Monitors;

using XboxBatteryMonitor.Core;

/// <summary>单次 XInput 电量查询结果。</summary>
public readonly record struct XInputBatteryInfo(XInputBatteryType Type, XInputBatteryLevel Level);

/// <summary>xinput1_4.dll 的可注入抽象（测试用 Fake 替换）。</summary>
public interface IXInputApi : IDisposable
{
    /// <summary>xinput1_4.dll 是否存在且可调用（不可用则整个 XInput 通道禁用）。</summary>
    bool IsAvailable { get; }

    /// <summary>槽位是否有手柄连接（XInputGetState 返回 ERROR_SUCCESS）。</summary>
    bool IsConnected(int userIndex);

    /// <summary>查询手柄本体电量（BATTERY_DEVTYPE_GAMEPAD）。</summary>
    XInputBatteryInfo GetBatteryInformation(int userIndex);

    /// <summary>设置左右马达转速（0-65535）。</summary>
    void SetVibration(int userIndex, ushort leftMotorSpeed, ushort rightMotorSpeed);
}
