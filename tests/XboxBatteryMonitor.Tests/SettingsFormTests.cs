namespace XboxBatteryMonitor.Tests;

using System.Drawing;
using System.Windows.Forms;
using XboxBatteryMonitor.Core;
using XboxBatteryMonitor.UI;
using Xunit;

public class SettingsFormTests
{
    [Fact]
    public void ApplyDpiScale_175Percent_ScalesWholeTreeProportionally()
    {
        // 4K 175% 缩放 = 168 DPI：窗体与控件坐标/尺寸均匀 ×1.75
        //（字体由 DPI 感知进程自行放大，不在此断言）
        using var form = new SettingsForm(new AppSettings(20, 10, true));
        var warning = (NumericUpDown)form.Controls[1];

        form.ApplyDpiScale(168);

        Assert.Equal(new Size(578, 368), form.ClientSize);            // 330×210 ×1.75
        Assert.Equal(new Point(280, 21), warning.Location);           // (160,12) ×1.75
        // NumericUpDown 缩放后会被内部布局按宿主环境字体度量轻微收缩（96 DPI 测试宿主 102，真机 168 DPI 105），取范围
        Assert.InRange(warning.Width, 100, 105);
        Button save = form.Controls.OfType<Button>().Single(b => b.Text == "保存");
        Assert.Equal(new Rectangle(280, 264, 131, 40), save.Bounds);  // (160,151,75,23) ×1.75
    }

    [Fact]
    public void ApplyDpiScale_96Dpi_NoOp()
    {
        using var form = new SettingsForm(new AppSettings(20, 10, true));
        form.ApplyDpiScale(96);
        Assert.Equal(new Size(330, 210), form.ClientSize);
    }

    [Fact]
    public void TrayStyleCombo_ReflectsSetting_AndBuildSettingsCarriesIt()
    {
        using var form = new SettingsForm(new AppSettings(20, 10, true, TrayIconStyle.Percent));
        var combo = form.Controls.OfType<ComboBox>().Single();
        Assert.Equal(1, combo.SelectedIndex);   // 电池图形=0 / 电量数值=1 / 图形+数值=2

        combo.SelectedIndex = 2;
        Assert.Equal(new AppSettings(20, 10, true, TrayIconStyle.BatteryAndPercent), form.BuildSettings());
    }
}
