namespace XboxBatteryMonitor.UI;

using System.Drawing;
using System.Windows.Forms;
using XboxBatteryMonitor.Core;

/// <summary>设置窗体：警告/危险阈值 + 托盘图标样式 + 震动开关。保存时校验 0 &lt; 危险 &lt; 警告 &lt; 100。</summary>
public sealed class SettingsForm : Form
{
    /// <summary>托盘图标样式选项，索引必须与 TrayIconStyle 枚举值一一对应。</summary>
    internal static readonly string[] TrayStyleLabels = { "电池图形", "电量数值", "图形+数值" };

    private readonly NumericUpDown _warning;
    private readonly NumericUpDown _critical;
    private readonly CheckBox _rumble;
    private readonly ComboBox _trayStyle;

    /// <summary>点击保存且校验通过时的最新设置；取消/关闭时为 null。</summary>
    public AppSettings? SavedSettings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Text = "XboxControllerBatteryMonitor 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;   // 供 PMv2 运行时跨屏 DPI 变化重缩放
        ClientSize = new Size(330, 210);

        var warningLabel = new Label { Text = "警告阈值（%）", Location = new Point(12, 15), AutoSize = true };
        _warning = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 98,
            Value = Math.Min(Math.Max(current.WarningThreshold, 1), 98),
            Location = new Point(160, 12),
            Width = 60,
        };
        var criticalLabel = new Label { Text = "危险阈值（%）", Location = new Point(12, 45), AutoSize = true };
        _critical = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 98,
            Value = Math.Min(Math.Max(current.CriticalThreshold, 1), 98),
            Location = new Point(160, 42),
            Width = 60,
        };
        _rumble = new CheckBox
        {
            Text = "低电量时手柄震动提醒",
            Location = new Point(12, 78),
            AutoSize = true,
            Checked = current.RumbleEnabled,
        };
        var styleLabel = new Label { Text = "托盘图标样式", Location = new Point(12, 111), AutoSize = true };
        _trayStyle = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(160, 105),
            Width = 120,
        };
        _trayStyle.Items.AddRange(TrayStyleLabels);
        _trayStyle.SelectedIndex = (int)current.TrayIconStyle;   // 设置加载前已经过 Sanitized，枚举值必有对应项
        var save = new Button { Text = "保存", Location = new Point(160, 151), Width = 75 };
        var cancel = new Button { Text = "取消", Location = new Point(245, 151), Width = 75 };

        save.Click += OnSave;
        cancel.Click += (_, _) => Close();
        AcceptButton = save;
        CancelButton = cancel;

        Controls.AddRange(new Control[]
        {
            warningLabel, _warning, criticalLabel, _critical, _rumble, styleLabel, _trayStyle, save, cancel,
        });
        ApplyDpiScale();
    }

    /// <summary>
    /// 以 96 DPI 为基准按设备 DPI 缩放整个控件树。手写窗体必须显式缩放：
    /// WinForms 会把 AutoScaleDimensions 重新捕获为当前 DPI（实测 .NET 8 168 DPI 下读回 168 而非 96），
    /// 框架自动缩放系数恒为 1；字体由 DPI 感知进程随 DPI 放大，无需在此处理。
    /// </summary>
    internal void ApplyDpiScale(int? deviceDpi = null)
    {
        float factor = (deviceDpi ?? DeviceDpi) / 96f;
        if (factor != 1f) Scale(new SizeF(factor, factor));
    }

    /// <summary>从控件当前值组装待校验的设置（供保存与测试复用）。</summary>
    internal AppSettings BuildSettings() => new(
        (int)_warning.Value, (int)_critical.Value, _rumble.Checked, (TrayIconStyle)_trayStyle.SelectedIndex);

    private void OnSave(object? sender, EventArgs e)
    {
        AppSettings candidate = BuildSettings();
        if (!candidate.IsValid)
        {
            MessageBox.Show(this, "阈值需满足：0 < 危险阈值 < 警告阈值 < 100", "无效设置",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SavedSettings = candidate;
        DialogResult = DialogResult.OK;
        Close();
    }
}
