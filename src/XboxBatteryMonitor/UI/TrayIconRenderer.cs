namespace XboxBatteryMonitor.UI;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

/// <summary>一次图标绘制的输入。Percent 为 null 且无特殊标记时只画空电池轮廓。</summary>
public sealed record IconSpec(
    int? Percent,
    bool Waiting,
    bool Wired,
    bool NoConnection,
    int DisplayIndex,
    bool LightTheme);

public static class TrayIconRenderer
{
    /// <summary>
    /// 以系统小图标尺寸绘制托盘图标（托盘槽位随 DPI 变大：96 DPI→16，192→32，300%→48）。
    /// </summary>
    public static Bitmap Draw(IconSpec spec) => Draw(spec, GetTrayIconSize());

    /// <summary>
    /// 按指定像素尺寸绘制托盘图标。布局以 32x32 为逻辑坐标：电池轮廓 (5,8)-(25,28)，
    /// 内部填充区 x=7..23/y=10..26，正极头 (11,3) 10x5；等待/有线/无连接在电池中央画字母；
    /// 多手柄时右下角橙色角标。size≠32 时整体等比缩放，保证高 DPI 下原生分辨率绘制不模糊。
    /// </summary>
    public static Bitmap Draw(IconSpec spec, int size)
    {
        var bmp = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        if (size != 32) g.ScaleTransform(size / 32f, size / 32f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Color foreground = spec.LightTheme ? Color.Black : Color.White;   // 浅色任务栏用深色图标
        using var outline = new Pen(foreground, 2);
        using var fill = new SolidBrush(foreground);

        g.FillRectangle(fill, 11, 3, 10, 5);        // 正极头
        g.DrawRectangle(outline, 5, 8, 20, 20);     // 电池轮廓

        if (spec.Percent is int p && p > 0)
        {
            const int innerHeight = 16;             // 填充区 y=10..26
            int fillHeight = Math.Max(1, (int)Math.Round(innerHeight * p / 100.0));
            g.FillRectangle(fill, 7, 26 - fillHeight, 16, fillHeight);
        }

        if (spec.NoConnection) DrawGlyph(g, foreground, "✕", 15, 18, 12);
        else if (spec.Waiting) DrawGlyph(g, foreground, "?", 15, 18, 12);
        else if (spec.Wired) DrawGlyph(g, foreground, "w", 15, 18, 12);

        if (spec.DisplayIndex > 1)
        {
            using var badge = new SolidBrush(Color.OrangeRed);
            g.FillEllipse(badge, 18, 18, 13, 13);
            DrawGlyph(g, Color.White, spec.DisplayIndex.ToString(), 24, 24, 10);
        }
        return bmp;
    }

    private static void DrawGlyph(Graphics g, Color color, string text, int centerX, int centerY, int pixelSize)
    {
        using var font = new Font("Segoe UI", pixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        SizeF measured = g.MeasureString(text, font);
        g.DrawString(text, font, brush, centerX - measured.Width / 2f, centerY - measured.Height / 2f);
    }

    /// <summary>Bitmap → Icon。克隆出独立 Icon 并释放临时 HICON。</summary>
    public static Icon ToIcon(Bitmap bitmap)
    {
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    /// <summary>释放 Icon 及其底层句柄（每次换图标都必须调用，防 GDI 泄漏）。</summary>
    public static void DisposeIcon(Icon? icon)
    {
        if (icon is null) return;
        IntPtr handle = icon.Handle;
        icon.Dispose();
        if (handle != IntPtr.Zero) Native.DestroyIcon(handle);
    }

    /// <summary>系统小图标尺寸（托盘槽位实际大小）：96 DPI→16，144→24，192→32，更高缩放→48。异常返回值回退 32。</summary>
    public static int GetTrayIconSize()
    {
        int small = Native.GetSystemMetrics(Native.SM_CXSMICON);
        return small is >= 16 and <= 64 ? small : 32;
    }

    private static class Native
    {
        public const int SM_CXSMICON = 49;

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);
    }
}

/// <summary>系统主题检测（浅色主题 → 任务栏浅色背景 → 图标用深色前景）。</summary>
public static class ThemeHelper
{
    public static bool IsLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value ? value == 1 : true;
        }
        catch
        {
            return true;
        }
    }
}
