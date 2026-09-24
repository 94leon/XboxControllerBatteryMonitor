namespace XboxBatteryMonitor.UI;

using System.Drawing;
using System.Windows.Forms;
using XboxBatteryMonitor.Core;

/// <summary>设置窗体：警告/危险阈值 + 震动开关。保存时校验 0 &lt; 危险 &lt; 警告 &lt; 100。</summary>
public sealed class SettingsForm : Form
{
    private readonly NumericUpDown _warning;
    private readonly NumericUpDown _critical;
    private readonly CheckBox _rumble;

    /// <summary>点击保存且校验通过时的最新设置；取消/关闭时为 null。</summary>
    public AppSettings? SavedSettings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Text = "XboxBatteryMonitor 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(330, 170);

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
        var save = new Button { Text = "保存", Location = new Point(160, 125), Width = 75 };
        var cancel = new Button { Text = "取消", Location = new Point(245, 125), Width = 75 };

        save.Click += OnSave;
        cancel.Click += (_, _) => Close();
        AcceptButton = save;
        CancelButton = cancel;

        Controls.AddRange(new Control[] { warningLabel, _warning, criticalLabel, _critical, _rumble, save, cancel });
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var candidate = new AppSettings((int)_warning.Value, (int)_critical.Value, _rumble.Checked);
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
