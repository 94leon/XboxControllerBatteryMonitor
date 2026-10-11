namespace XboxBatteryMonitor.Tests;

using System.Drawing;
using XboxBatteryMonitor.Core;
using XboxBatteryMonitor.UI;
using Xunit;

public class TrayIconRendererTests : IDisposable
{
    private readonly List<Bitmap> _bitmaps = new();

    private Bitmap Draw(IconSpec spec, int size = 32)
    {
        Bitmap bmp = TrayIconRenderer.Draw(spec, size);
        _bitmaps.Add(bmp);
        return bmp;
    }

    private static int CountDarkPixels(Bitmap bmp, int x0, int y0, int x1, int y1)
    {
        int count = 0;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.R < 80 && c.G < 80 && c.B < 80) count++;
            }
        return count;
    }

    [Fact]
    public void FullBattery_FillsTopOfBattery()
    {
        Bitmap bmp = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: true));
        // 电池内部填充区 x=7..23, y=10..26；满电时顶部 (10..20, 11) 应为深色
        Assert.True(CountDarkPixels(bmp, 10, 11, 20, 11) >= 5);
    }

    [Fact]
    public void EmptyBattery_DoesNotFillTop()
    {
        Bitmap bmp = Draw(new IconSpec(5, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: true));
        Assert.Equal(0, CountDarkPixels(bmp, 10, 11, 20, 13));  // 5% 只填底部 1px
    }

    [Fact]
    public void NoConnection_OnlyGlyphNoFill()
    {
        Bitmap noConn = Draw(new IconSpec(null, Waiting: false, Wired: false, NoConnection: true, DisplayIndex: 1, LightTheme: true));
        Bitmap full = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: true));
        // 无连接时内部只有中央 ✕ 字形，没有整片电量填充 → 深色像素远少于满电
        int noConnInterior = CountDarkPixels(noConn, 9, 12, 21, 25);
        int fullInterior = CountDarkPixels(full, 9, 12, 21, 25);
        Assert.True(noConnInterior < fullInterior / 2, $"noConn={noConnInterior}, full={fullInterior}");
        Assert.True(CountDarkPixels(noConn, 4, 8, 4, 28) > 0);   // 左侧轮廓线存在
    }

    [Fact]
    public void DarkTheme_DrawsLightForeground()
    {
        Bitmap bmp = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: false));
        Color c = bmp.GetPixel(15, 11);
        Assert.True(c.A > 0 && c.R > 200 && c.G > 200 && c.B > 200);   // 浅色填充
    }

    private static int CountOrangePixels(Bitmap bmp) => CountOrangePixels(bmp, 19, 19, 30, 30);

    private static int CountOrangePixels(Bitmap bmp, int x0, int y0, int x1, int y1)
    {
        int count = 0;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.R > 200 && c.G < 120 && c.B < 120) count++;   // OrangeRed 色系
            }
        return count;
    }

    [Fact]
    public void Draw_LargerSize_ScalesLayoutProportionally()
    {
        // 高分屏按系统小图标尺寸原生绘制：48px 时电池轮廓与角标按 1.5 倍比例出现在缩放后位置
        Bitmap bmp = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 3, LightTheme: true), 48);
        Assert.Equal(48, bmp.Width);
        Assert.Equal(48, bmp.Height);
        // 左轮廓逻辑 x=5..7 ×1.5 → 约 8..11
        Assert.True(CountDarkPixels(bmp, 6, 16, 12, 36) > 0, "放大绘制后电池左轮廓缺失");
        // 角标逻辑 (18,18)-(31,31) ×1.5 → 约 27..47
        Assert.True(CountOrangePixels(bmp, 28, 28, 46, 46) > 20, "放大绘制后角标缺失");
    }

    [Fact]
    public void GetTrayIconSize_ReturnsSensibleSize()
    {
        // 托盘槽位 = 系统小图标尺寸（96 DPI→16，192→32，300%→48）；异常值需回退到 32
        int size = TrayIconRenderer.GetTrayIconSize();
        Assert.InRange(size, 16, 64);
    }

    [Fact]
    public void DisplayIndex_DrawsBadge()
    {
        Bitmap bmp = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 3, LightTheme: true));
        // 右下角角标：橙色圆 + 白色数字，区域统计避开采样点恰好落在数字笔画上的脆弱性
        Assert.True(CountOrangePixels(bmp) > 20);
    }

    [Fact]
    public void SingleController_NoBadge()
    {
        Bitmap bmp = Draw(new IconSpec(100, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: true));
        Assert.Equal(0, CountOrangePixels(bmp));   // 无角标颜色
    }

    private static IconSpec StyleSpec(int? percent, TrayIconStyle style, int warning = 20, int critical = 10) =>
        new(percent, Waiting: false, Wired: false, NoConnection: false, DisplayIndex: 1, LightTheme: true,
            Style: style, WarningThreshold: warning, CriticalThreshold: critical);

    private static int CountRedPixels(Bitmap bmp, int x0, int y0, int x1, int y1)
    {
        int count = 0;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.R > 170 && c.G < 120 && c.B < 140) count++;   // 危险档红 (229,57,70)
            }
        return count;
    }

    private static int CountAmberPixels(Bitmap bmp)
    {
        int count = 0;
        for (int x = 2; x <= 30; x++)
            for (int y = 2; y <= 30; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.R > 190 && c.G is > 110 and < 200 && c.B < 110) count++;   // 警告档琥珀 (224,140,20)
            }
        return count;
    }

    [Fact]
    public void PercentStyle_DrawsNumberWithoutBatteryShell()
    {
        Bitmap five = Draw(StyleSpec(5, TrayIconStyle.Percent));   // 5% 低于危险阈值 → 红色数字
        // 电池壳（左轮廓竖线）是主题前景色；纯数值模式没有壳 → 左缘深色像素应≈0
        Assert.True(CountDarkPixels(five, 3, 8, 8, 28) < 5, "纯数值模式不应画电池左轮廓");
        Assert.True(CountRedPixels(five, 6, 4, 26, 26) > 15, "纯数值模式红色数字缺失");

        Bitmap eightyFive = Draw(StyleSpec(85, TrayIconStyle.Percent));
        Assert.True(CountDarkPixels(eightyFive, 4, 6, 28, 26) > 40, "纯数值模式正常档大数字缺失");
    }

    [Fact]
    public void BatteryAndPercentStyle_OutlineAndNumber_NoFillBar()
    {
        Bitmap combined = Draw(StyleSpec(100, TrayIconStyle.BatteryAndPercent));
        Assert.True(CountDarkPixels(combined, 3, 8, 8, 28) > 3, "图形+数值模式电池左轮廓缺失");
        // 满电时电池模式内部顶部两行是实心填充；图形+数值模式去掉填充柱，该区域应无像素
        Assert.Equal(0, CountDarkPixels(combined, 9, 10, 23, 11));
        Assert.True(CountDarkPixels(combined, 8, 12, 24, 26) > 12, "图形+数值模式中央数字缺失");
    }

    [Fact]
    public void TierColors_FollowThresholds()
    {
        Bitmap amber = Draw(StyleSpec(40, TrayIconStyle.Percent, warning: 50, critical: 25));
        Assert.True(CountAmberPixels(amber) > 12, "低于警告阈值应为琥珀色");
        Bitmap red = Draw(StyleSpec(20, TrayIconStyle.Percent, warning: 50, critical: 25));
        Assert.True(CountRedPixels(red, 2, 2, 30, 30) > 12, "低于危险阈值应为红色");
        Bitmap normal = Draw(StyleSpec(60, TrayIconStyle.Percent, warning: 50, critical: 25));
        Assert.Equal(0, CountAmberPixels(normal));
        Assert.Equal(0, CountRedPixels(normal, 2, 2, 30, 30));
    }

    [Fact]
    public void PercentStyle_Waiting_ShowsQuestionGlyph()
    {
        Bitmap bmp = Draw(new IconSpec(null, Waiting: true, Wired: false, NoConnection: false,
            DisplayIndex: 1, LightTheme: true, Style: TrayIconStyle.Percent));
        Assert.True(CountDarkPixels(bmp, 8, 8, 24, 24) > 10, "等待读数时应显示问号字形");
    }

    // —— 数字最大化：托盘图标是 16~48px 的小画幅，数字必须占满可用空间才可读 ——

    /// <summary>扫描整幅位图中不透明度 ≥ minAlpha 的墨迹外接框（数字最大化测试用）。</summary>
    private static (int X0, int Y0, int X1, int Y1)? InkBounds(Bitmap bmp, int minAlpha = 64)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (bmp.GetPixel(x, y).A >= minAlpha)
                {
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
        return x1 < 0 ? null : (x0, y0, x1, y1);
    }

    private static string Describe((int X0, int Y0, int X1, int Y1)? box) =>
        box is null ? "无墨迹" : $"({box.Value.X0},{box.Value.Y0})-({box.Value.X1},{box.Value.Y1})" +
            $" 高{box.Value.Y1 - box.Value.Y0 + 1} 宽{box.Value.X1 - box.Value.X0 + 1}";

    [Fact]
    public void PercentStyle_SingleDigit_InkFillsCanvasHeight()
    {
        // 一位数没有宽度压力，墨迹高度应占满 32px 画布（≥24px），不能是只占一半的小字
        Bitmap bmp = Draw(StyleSpec(7, TrayIconStyle.Percent));
        var ink = InkBounds(bmp);
        Assert.True(ink is not null && ink.Value.Y1 - ink.Value.Y0 + 1 >= 24,
            $"一位数墨迹高度不足：{Describe(ink)}");
        Assert.True(ink.Value.X1 - ink.Value.X0 + 1 >= 14, $"一位数墨迹宽度过窄：{Describe(ink)}");
    }

    [Fact]
    public void PercentStyle_TwoDigits_InkFillsCanvasWidth()
    {
        // 两位数受宽度约束：墨迹应顶到画布左右边缘（≥24px 宽），高度也随之 ≥17px，且不越界
        Bitmap bmp = Draw(StyleSpec(85, TrayIconStyle.Percent));
        var ink = InkBounds(bmp);
        Assert.True(ink is not null && ink.Value.X1 - ink.Value.X0 + 1 >= 24,
            $"两位数墨迹宽度过窄：{Describe(ink)}");
        Assert.True(ink.Value.Y1 - ink.Value.Y0 + 1 >= 17, $"两位数墨迹高度不足：{Describe(ink)}");
        Assert.True(ink.Value.X0 >= 0 && ink.Value.X1 <= 31 && ink.Value.Y0 >= 0 && ink.Value.Y1 <= 31,
            $"两位数墨迹越界：{Describe(ink)}");
    }

    [Fact]
    public void PercentStyle_ThreeDigits_FitWithinCanvas()
    {
        // "100" 是唯一三位数：宽度优先收缩但仍应顶满画布宽度（≥26px），高度 ≥12px
        Bitmap bmp = Draw(StyleSpec(100, TrayIconStyle.Percent));
        var ink = InkBounds(bmp);
        Assert.True(ink is not null && ink.Value.X1 - ink.Value.X0 + 1 >= 26,
            $"三位数墨迹宽度过窄：{Describe(ink)}");
        Assert.True(ink.Value.Y1 - ink.Value.Y0 + 1 >= 12, $"三位数墨迹高度不足：{Describe(ink)}");
        Assert.True(ink.Value.X0 >= 0 && ink.Value.X1 <= 31 && ink.Value.Y0 >= 0 && ink.Value.Y1 <= 31,
            $"三位数墨迹越界：{Describe(ink)}");
    }

    [Fact]
    public void PercentStyle_NumberAntialiasing_IsGrayscale()
    {
        // 图标底是透明的（实际显示时叠在任务栏上）：ClearType 的亚像素彩边在任意底色上都发脏，
        // 必须用灰度抗锯齿——半透明像素的 RGB 三通道应一致
        Bitmap bmp = Draw(StyleSpec(85, TrayIconStyle.Percent));
        int coloredFringes = 0;
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
            {
                Color c = bmp.GetPixel(x, y);
                int spread = Math.Max(Math.Abs(c.R - c.G), Math.Max(Math.Abs(c.R - c.B), Math.Abs(c.G - c.B)));
                if (c.A is > 0 and < 255 && spread > 24) coloredFringes++;
            }
        Assert.Equal(0, coloredFringes);
    }

    [Fact]
    public void BatteryAndPercent_Number_InkFillsBatteryInterior()
    {
        // 图形+数值样式：数字要占满电池内部（x7..23/y10..26），且不得溢出到电池壳外
        Bitmap bmp = Draw(StyleSpec(85, TrayIconStyle.BatteryAndPercent));
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (int x = 7; x <= 23; x++)
            for (int y = 10; y <= 26; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A >= 64 && c.R < 80 && c.G < 80 && c.B < 80)
                {
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
            }
        var ink = x1 < 0 ? null : ((int X0, int Y0, int X1, int Y1)?)(x0, y0, x1, y1);
        Assert.True(ink is not null && ink.Value.Y1 - ink.Value.Y0 + 1 >= 10,
            $"电池内数字墨迹高度不足：{Describe(ink)}");
        Assert.True(ink.Value.X1 - ink.Value.X0 + 1 >= 13, $"电池内数字墨迹宽度过窄：{Describe(ink)}");
        Assert.True(ink.Value.X0 >= 6 && ink.Value.X1 <= 24 && ink.Value.Y0 >= 9 && ink.Value.Y1 <= 27,
            $"电池内数字溢出电池内部：{Describe(ink)}");
    }

    [Fact]
    public void ChooseFontStyle_SwitchesToRegularBelow12PhysicalPx()
    {
        // 16px 托盘上三位数 em≈9.5px、125% 缩放 20px 上 em≈11.9px：Bold 笔画会把 0/8 字腔糊死 → Regular
        Assert.Equal(FontStyle.Regular, TrayIconRenderer.ChooseFontStyle(9.5f));
        Assert.Equal(FontStyle.Regular, TrayIconRenderer.ChooseFontStyle(11.9f));
        // 常规尺寸（16px 托盘的两位数 em≈13.6px，24px+ 的所有数字）保持 Bold
        Assert.Equal(FontStyle.Bold, TrayIconRenderer.ChooseFontStyle(12f));
        Assert.Equal(FontStyle.Bold, TrayIconRenderer.ChooseFontStyle(13.6f));
        Assert.Equal(FontStyle.Bold, TrayIconRenderer.ChooseFontStyle(42f));
    }

    public void Dispose()
    {
        foreach (Bitmap b in _bitmaps) b.Dispose();
    }
}
