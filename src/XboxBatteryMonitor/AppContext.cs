namespace XboxBatteryMonitor;

using System.Globalization;
using System.Windows.Forms;
using Microsoft.Win32;
using XboxBatteryMonitor.Core;
using XboxBatteryMonitor.Monitors;
using XboxBatteryMonitor.UI;

/// <summary>组装层：监控器 → 聚合 → 阈值 → 托盘图标/菜单/Toast/震动。</summary>
public sealed class AppContext : ApplicationContext
{
    private const int IconCycleMs = 5000;   // 多手柄图标轮换间隔

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxBatteryMonitor");

    private readonly SynchronizationContext _ui;
    private readonly string _settingsPath = Path.Combine(DataDir, "settings.json");
    private AppSettings _settings;
    private readonly ThresholdDetector _detector;
    private readonly XInputApi _xinputApi = new();
    private readonly XInputMonitor _xinput;
    private readonly BleMonitor _ble = new();
    private readonly RumbleNotifier _rumble;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _cycleTimer = new() { Interval = IconCycleMs };
    private IReadOnlyList<ControllerStatus> _controllers = Array.Empty<ControllerStatus>();
    private Icon? _currentIcon;
    private readonly FormReuseGuard _settingsGuard = new();
    private int _cycleIndex;
    private bool _disposed;

    public AppContext()
    {
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _settings = SettingsStore.Load(_settingsPath);
        _detector = new ThresholdDetector(_settings);
        _xinput = new XInputMonitor(_xinputApi);
        _rumble = new RumbleNotifier(_xinput);

        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "XboxBatteryMonitor",
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        _xinput.SnapshotChanged += OnMonitorChanged;   // 后台线程 → 封送回 UI
        _ble.SnapshotChanged += OnMonitorChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _cycleTimer.Tick += (_, _) => UpdateTray();
        _cycleTimer.Start();

        _xinput.PollOnce();   // 启动即出首帧，不等 2s 定时器
        Recalculate();
    }

    private void OnMonitorChanged() => _ui.Post(_ => Recalculate(), null);

    /// <summary>核心刷新：合并快照 → 阈值检测 → 通知 → 更新菜单和图标。仅在 UI 线程调用。</summary>
    private void Recalculate()
    {
        IReadOnlyList<ControllerStatus> next =
            Aggregator.Aggregate(_xinput.CurrentSnapshot, _ble.CurrentSnapshot);

        foreach (ControllerStatus gone in _controllers.Where(p => next.All(n => n.Id != p.Id)))
            _detector.Reset(gone.Id);                      // 手柄消失 → 重置阈值状态

        _controllers = next;

        List<BatteryAlert> alerts = next.SelectMany(_detector.Process).ToList();
        foreach (BatteryAlert alert in ThresholdDetector.MostSeverePerController(alerts))
        {
            string body = alert.Tier == AlertTier.Critical
                ? $"电量严重不足（{alert.Percent}%），请立即充电"
                : $"电量低（{alert.Percent}%）";
            ToastNotifier.Show(alert.DisplayName, body);
            if (_settings.RumbleEnabled)
                NotifyRumble(alert.ControllerId);
        }

        RebuildMenu();
        UpdateTray();
    }

    /// <summary>震动目标选择（规格 §7）：XInput 来源精确到槽位；蓝牙手柄仅唯一无线槽位时震动。</summary>
    private void NotifyRumble(string controllerId)
    {
        if (!_xinput.IsAvailable) return;

        if (controllerId.StartsWith("xi:", StringComparison.Ordinal))
        {
            int slot = int.Parse(controllerId[3..], CultureInfo.InvariantCulture);
            _ = _rumble.RumbleAsync(slot);
            return;
        }

        XInputSlotSnapshot[] wireless = _xinput.CurrentSnapshot
            .Where(s => s.BatteryType is XInputBatteryType.Alkaline or XInputBatteryType.Nimh or XInputBatteryType.Disconnected)
            .ToArray();
        if (wireless.Length == 1)
            _ = _rumble.RumbleAsync(wireless[0].Slot);
        // 多无线手柄且低电量的是蓝牙手柄：槽位有歧义，只发 Toast 不震动
    }

    private void RebuildMenu()
    {
        ContextMenuStrip menu = _tray.ContextMenuStrip!;
        menu.Items.Clear();
        foreach (ControllerStatus c in _controllers)
            menu.Items.Add(new ToolStripMenuItem($"{c.DisplayName}（{KindLabel(c.Kind)}）{DescribeBattery(c)}") { Enabled = false });
        if (_controllers.Count > 0)
            menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("设置…");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(exitItem);
    }

    private void UpdateTray()
    {
        IconSpec spec;
        if (_controllers.Count == 0)
        {
            spec = new IconSpec(null, Waiting: false, Wired: false, NoConnection: true,
                DisplayIndex: 1, LightTheme: ThemeHelper.IsLightTheme());
        }
        else
        {
            int index = _cycleIndex % _controllers.Count;
            ControllerStatus current = _controllers[index];
            spec = new IconSpec(
                Percent: current.Battery.ToEstimatedPercent(),
                Waiting: current.Battery is BatteryValue.Waiting,
                Wired: current.Battery is BatteryValue.Wired,
                NoConnection: false,
                DisplayIndex: _controllers.Count > 1 ? index + 1 : 1,
                LightTheme: ThemeHelper.IsLightTheme());
        }
        _cycleIndex++;

        using Bitmap bitmap = TrayIconRenderer.Draw(spec);
        Icon nextIcon = TrayIconRenderer.ToIcon(bitmap);
        _tray.Icon = nextIcon;
        TrayIconRenderer.DisposeIcon(_currentIcon);   // 防 GDI 句柄泄漏（规格 §8）
        _currentIcon = nextIcon;
        _tray.Text = BuildTooltip(_controllers, _ble.IsBluetoothOn, _xinput.IsAvailable);
    }

    private void ShowSettings()
    {
        if (_settingsGuard.Current is { } open)
        {
            open.Activate();   // 已打开 → 置前而非新开（模态循环中托盘菜单仍可点击）
            return;
        }

        using var form = new SettingsForm(_settings);
        _settingsGuard.Register(form);
        try
        {
            if (form.ShowDialog() == DialogResult.OK && form.SavedSettings is AppSettings saved)
            {
                _settings = saved;
                _detector.UpdateSettings(saved);
                SettingsStore.Save(_settingsPath, saved);
                Recalculate();
            }
        }
        finally
        {
            _settingsGuard.Release(form);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;   // 休眠恢复 → 强制全量刷新（规格 §8）
        _ui.Post(_ =>
        {
            _xinput.PollOnce();
            _ = _ble.DiscoverAsync();
        }, null);
    }

    internal static string KindLabel(ConnectionKind kind) => kind switch
    {
        ConnectionKind.Bluetooth => "蓝牙",
        ConnectionKind.Adapter => "适配器",
        _ => "USB",
    };

    internal static string DescribeBattery(ControllerStatus c) => c.Battery switch
    {
        BatteryValue.Percent p => $"{p.Value}%",
        BatteryValue.Level l => $"{l.ToEstimatedPercent() ?? 0}%（估算）",
        BatteryValue.Wired => "有线",
        _ => "等待读数",
    };

    internal static string BuildTooltip(IReadOnlyList<ControllerStatus> controllers, bool bluetoothOn, bool xinputAvailable)
    {
        string text;
        if (controllers.Count == 0)
            text = !bluetoothOn && !xinputAvailable ? "XboxBatteryMonitor（无可用通道）"
                 : !bluetoothOn ? "XboxBatteryMonitor（蓝牙已关闭）"
                 : "XboxBatteryMonitor（未发现手柄）";
        else
            text = string.Join(" | ", controllers.Select(c => $"{c.DisplayName} {DescribeBattery(c)}"));

        return text.Length <= 63 ? text : text[..60] + "…";   // NotifyIcon.Text 上限
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed || !disposing) return;
        _disposed = true;

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _cycleTimer.Dispose();
        _xinput.SnapshotChanged -= OnMonitorChanged;
        _ble.SnapshotChanged -= OnMonitorChanged;
        _xinput.Dispose();
        _xinputApi.Dispose();
        _ble.Dispose();
        _tray.Visible = false;
        TrayIconRenderer.DisposeIcon(_currentIcon);
        _tray.Dispose();
        base.Dispose(disposing);
    }
}
