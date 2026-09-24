namespace XboxBatteryMonitor.Monitors;

using System.Runtime.InteropServices;
using XboxBatteryMonitor.Core;

/// <summary>P/Invoke 真实实现。DLL 缺失时 IsAvailable=false，其余方法不会被调用。</summary>
public sealed class XInputApi : IXInputApi
{
    private bool _available;
    private bool _availabilityChecked;

    public bool IsAvailable
    {
        get
        {
            if (!_availabilityChecked)
            {
                try
                {
                    Native.XInputGetState(0, out _);
                    _available = true;
                }
                catch (DllNotFoundException)
                {
                    _available = false;
                }
                _availabilityChecked = true;
            }
            return _available;
        }
    }

    public bool IsConnected(int userIndex) => Native.XInputGetState((uint)userIndex, out _) == 0; // ERROR_SUCCESS

    public XInputBatteryInfo GetBatteryInformation(int userIndex)
    {
        var info = new XInputBatteryInformation();
        Native.XInputGetBatteryInformation((uint)userIndex, 0 /* BATTERY_DEVTYPE_GAMEPAD */, ref info);
        return new XInputBatteryInfo((XInputBatteryType)info.BatteryType, (XInputBatteryLevel)info.BatteryLevel);
    }

    public void SetVibration(int userIndex, ushort left, ushort right)
    {
        var vibration = new XInputVibration { LeftMotorSpeed = left, RightMotorSpeed = right };
        Native.XInputSetState((uint)userIndex, ref vibration);
    }

    public void Dispose() { }

    private static class Native
    {
        private const string Dll = "xinput1_4.dll";

        [DllImport(Dll)]
        public static extern uint XInputGetState(uint dwUserIndex, out XInputState state);

        [DllImport(Dll)]
        public static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, ref XInputBatteryInformation batteryInformation);

        [DllImport(Dll)]
        public static extern uint XInputSetState(uint dwUserIndex, ref XInputVibration vibration);
    }

    // 结构体布局必须与 xinput.h 完全一致（按值封送时大小错误会破坏内存）
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }
}
