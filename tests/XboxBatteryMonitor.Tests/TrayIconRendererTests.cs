namespace XboxBatteryMonitor.Tests;

using System.Drawing;
using XboxBatteryMonitor.UI;
using Xunit;

public class TrayIconRendererTests : IDisposable
{
    private readonly List<Bitmap> _bitmaps = new();

    private Bitmap Draw(IconSpec spec)
    {
        Bitmap bmp = TrayIconRenderer.Draw(spec);
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

    private static int CountOrangePixels(Bitmap bmp)
    {
        int count = 0;
        for (int x = 19; x <= 30; x++)
            for (int y = 19; y <= 30; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.R > 200 && c.G < 120 && c.B < 120) count++;   // OrangeRed 色系
            }
        return count;
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

    public void Dispose()
    {
        foreach (Bitmap b in _bitmaps) b.Dispose();
    }
}
