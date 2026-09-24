namespace XboxBatteryMonitor.Monitors;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using XboxBatteryMonitor.Core;

/// <summary>
/// 蓝牙手柄电量监控：枚举已配对 BLE 手柄（外观筛选），轮询标准电量服务 0x180F/0x2A19。
/// 快照事件已封送回 UI 线程（构造时捕获的 SynchronizationContext）。
/// </summary>
public sealed class BleMonitor : IDisposable
{
    private static readonly Guid BatteryServiceUuid = new("0000180f-0000-1000-8000-00805f9b34fb");
    private static readonly Guid BatteryLevelUuid = new("00002a19-0000-1000-8000-00805f9b34fb");
    private const int PollIntervalMs = 10_000;
    private const int DiscoverIntervalMs = 60_000;
    private const int MaxMissedReads = 3;   // 连续失败 3 次 → 快照中标记离线（规格 §8）

    private readonly SynchronizationContext _ui;
    private readonly List<BluetoothLEDevice> _gamepads = new();
    private readonly Dictionary<string, int> _missedReads = new();
    private readonly Dictionary<string, int> _lastPercent = new();
    private readonly System.Threading.Timer _pollTimer;
    private readonly System.Threading.Timer _discoverTimer;
    private Radio? _bluetoothRadio;

    public event Action? SnapshotChanged;

    /// <summary>蓝牙无线电是否开启（无蓝牙适配器时为 false）。</summary>
    public bool IsBluetoothOn => _bluetoothRadio?.State == RadioState.On;

    public IReadOnlyList<BleDeviceSnapshot> CurrentSnapshot { get; private set; } = Array.Empty<BleDeviceSnapshot>();

    public BleMonitor()
    {
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _pollTimer = new System.Threading.Timer(_ => _ = PollAsync(), null, PollIntervalMs, PollIntervalMs);
        _discoverTimer = new System.Threading.Timer(_ => _ = DiscoverAsync(), null, 0, DiscoverIntervalMs);
    }

    /// <summary>枚举已配对 BLE 设备，筛出带电量服务的游戏手柄并订阅连接事件。</summary>
    public async Task DiscoverAsync()
    {
        try
        {
            Radio? radio = await EnsureRadioAsync();
            if (radio is null || radio.State != RadioState.On)
            {
                UpdateSnapshot(Array.Empty<BleDeviceSnapshot>());   // 蓝牙关闭/无适配器：通道整体不可用
                return;
            }

            string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);
            var found = new List<BluetoothLEDevice>();
            foreach (DeviceInformation info in devices)
            {
                BluetoothLEDevice? device = null;
                try { device = await BluetoothLEDevice.FromIdAsync(info.Id); }
                catch { continue; }                                    // 个别设备打不开：跳过（规格 §8）
                if (device is null) continue;
                if (device.Appearance?.SubCategory != BluetoothLEAppearanceSubcategories.Gamepad)
                {
                    device.Dispose();
                    continue;
                }
                found.Add(device);
            }

            foreach (BluetoothLEDevice old in _gamepads)
            {
                old.ConnectionStatusChanged -= OnConnectionChanged;
                old.Dispose();
            }
            _gamepads.Clear();
            _gamepads.AddRange(found);
            foreach (BluetoothLEDevice device in _gamepads)
                device.ConnectionStatusChanged += OnConnectionChanged;

            _missedReads.Clear();
            await PollAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("BLE 发现失败: " + ex.Message);
        }
    }

    /// <summary>轮询所有已连接手柄的电量。</summary>
    public async Task PollAsync()
    {
        try
        {
            foreach (BluetoothLEDevice device in _gamepads)
            {
                if (device.ConnectionStatus != BluetoothConnectionStatus.Connected) continue;
                int? percent = await ReadBatteryAsync(device);
                if (percent is int p)
                {
                    _missedReads[device.DeviceId] = 0;
                    _lastPercent[device.DeviceId] = Math.Clamp(p, 0, 100);
                }
                else
                {
                    _missedReads[device.DeviceId] =
                        _missedReads.TryGetValue(device.DeviceId, out int missed) ? missed + 1 : 1;
                }
            }
            RebuildSnapshot();
        }
        catch (Exception ex)
        {
            Logger.Error("BLE 轮询失败: " + ex.Message);
        }
    }

    private async Task<int?> ReadBatteryAsync(BluetoothLEDevice device)
    {
        try
        {
            using GattDeviceService? service = device.GetGattService(BatteryServiceUuid);
            if (service is null) return null;
            GattCharacteristic? characteristic = service.GetCharacteristics(BatteryLevelUuid).FirstOrDefault();
            if (characteristic is null) return null;
            GattReadResult result = await characteristic.ReadValueAsync();
            if (result.Status != GattCommunicationStatus.Success || result.Value.Length == 0) return null;
            return DataReader.FromBuffer(result.Value).ReadByte();   // 单字节百分比 0-100
        }
        catch
        {
            return null;
        }
    }

    private void RebuildSnapshot()
    {
        var snapshot = new List<BleDeviceSnapshot>();
        foreach (BluetoothLEDevice device in _gamepads)
        {
            bool connected = device.ConnectionStatus == BluetoothConnectionStatus.Connected
                && (!_missedReads.TryGetValue(device.DeviceId, out int missed) || missed < MaxMissedReads);
            int? percent = null;
            if (connected && _lastPercent.TryGetValue(device.DeviceId, out int last))
                percent = last;
            snapshot.Add(new BleDeviceSnapshot(device.DeviceId, device.Name, connected, percent));
        }
        UpdateSnapshot(snapshot);
    }

    private void OnConnectionChanged(BluetoothLEDevice sender, object args)
    {
        _missedReads[sender.DeviceId] = 0;
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            _lastPercent.Remove(sender.DeviceId);   // 重连视为新会话，等下一次读数
        _ = PollAsync();                            // 连接/断开即时刷新，不等 10s 轮询
    }

    private async Task<Radio?> EnsureRadioAsync()
    {
        if (_bluetoothRadio is not null) return _bluetoothRadio;
        var radios = await Radio.GetRadiosAsync();
        _bluetoothRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (_bluetoothRadio is not null)
            _bluetoothRadio.StateChanged += (_, _) => _ = DiscoverAsync();   // 蓝牙开关切换 → 重新发现
        return _bluetoothRadio;
    }

    private void UpdateSnapshot(IReadOnlyList<BleDeviceSnapshot> snapshot) =>
        _ui.Post(_ =>
        {
            CurrentSnapshot = snapshot;
            SnapshotChanged?.Invoke();
        }, null);

    public void Dispose()
    {
        _pollTimer.Dispose();
        _discoverTimer.Dispose();
        foreach (BluetoothLEDevice device in _gamepads)
        {
            device.ConnectionStatusChanged -= OnConnectionChanged;
            device.Dispose();
        }
        _gamepads.Clear();
    }
}
