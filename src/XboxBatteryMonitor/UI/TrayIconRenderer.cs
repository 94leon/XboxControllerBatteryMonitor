namespace XboxBatteryMonitor.UI;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using XboxBatteryMonitor.Core;

/// <summary>一次图标绘制的输入。Percent 为 null 且无特殊标记时只画空电池轮廓。</summary>
public sealed record IconSpec(
    int? Percent,
    bool Waiting,
    bool Wired,
    bool NoConnection,
    int DisplayIndex,
    bool LightTheme,
    TrayIconStyle Style = TrayIconStyle.Battery,
    int WarningThreshold = 20,
    int CriticalThreshold = 10);

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
    /// 样式（TrayIconStyle）：Battery 画填充柱；Percent 只画数字，按精确墨迹框放大到占满画幅；
    /// BatteryAndPercent 画轮廓 + 数字占满电池内部（不画填充柱，避免与数字重叠），
    /// 电量档位改由数字颜色表达（琥珀/红）。
    /// 文本用灰度抗锯齿（AntiAliasGridFit）：图标底是透明的，ClearType 亚像素彩边在任务栏上发虚。
    /// </summary>
    public static Bitmap Draw(IconSpec spec, int size)
    {
        var bmp = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        if (size != 32) g.ScaleTransform(size / 32f, size / 32f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        Color foreground = spec.LightTheme ? Color.Black : Color.White;   // 浅色任务栏用深色图标
        using var outline = new Pen(foreground, 2);
        using var fill = new SolidBrush(foreground);

        if (spec.Style == TrayIconStyle.Percent)
        {
            // 纯数值：无电池壳，数字放大到占满画幅（四周各留 1px）；颜色分档一眼可辨
            if (spec.Percent is int p)
                DrawNumber(g, TierColor(spec, foreground), p.ToString(), new RectangleF(1, 1, 30, 30));
            else if (spec.NoConnection) DrawGlyph(g, foreground, "✕", 16, 16, 24);
            else if (spec.Waiting) DrawGlyph(g, foreground, "?", 16, 16, 24);
            else if (spec.Wired) DrawGlyph(g, foreground, "w", 16, 16, 24);
        }
        else
        {
            g.FillRectangle(fill, 11, 3, 10, 5);        // 正极头
            g.DrawRectangle(outline, 5, 8, 20, 20);     // 电池轮廓

            if (spec.Style == TrayIconStyle.Battery)
            {
                if (spec.Percent is int p && p > 0)
                {
                    const int innerHeight = 16;             // 填充区 y=10..26
                    int fillHeight = Math.Max(1, (int)Math.Round(innerHeight * p / 100.0));
                    g.FillRectangle(fill, 7, 26 - fillHeight, 16, fillHeight);
                }
            }
            else if (spec.Percent is int p)
            {
                DrawNumber(g, TierColor(spec, foreground), p.ToString(), new RectangleF(7, 10, 16, 16));
            }

            if (spec.NoConnection) DrawGlyph(g, foreground, "✕", 15, 18, 12);
            else if (spec.Waiting) DrawGlyph(g, foreground, "?", 15, 18, 12);
            else if (spec.Wired) DrawGlyph(g, foreground, "w", 15, 18, 12);
        }

        if (spec.DisplayIndex > 1)
        {
            using var badge = new SolidBrush(Color.OrangeRed);
            g.FillEllipse(badge, 18, 18, 13, 13);
            DrawGlyph(g, Color.White, spec.DisplayIndex.ToString(), 24, 24, 10);
        }
        return bmp;
    }

    /// <summary>电量数字的档位颜色：低于危险阈值红、低于警告阈值琥珀、否则主题前景色。</summary>
    private static Color TierColor(IconSpec spec, Color foreground) => spec.Percent switch
    {
        int p when p < spec.CriticalThreshold => Color.FromArgb(229, 57, 70),
        int p when p < spec.WarningThreshold => Color.FromArgb(224, 140, 20),
        _ => foreground,
    };

    /// <summary>
    /// 把数字画到恰好装进 box 的最大字号：先用 GraphicsPath 在参考字号下测出精确墨迹外接框
    /// （MeasureString 默认带约 1/6 em 内边距，会把字号压小三成左右），字形轮廓随字号线性缩放，
    /// 故可直接按比例取最大整数字号；数字没有降部，按墨迹框而非字号框适配能吃满可用高度。
    /// 物理字号过小时 Bold 笔画会把 0/8 的字腔糊死（尤其 16px 托盘下的三位数），降为 Regular。
    /// </summary>
    private static void DrawNumber(Graphics g, Color color, string text, RectangleF box)
    {
        const float referenceEm = 100f;
        using var family = new FontFamily("Segoe UI");
        FontStyle style = FontStyle.Bold;
        RectangleF ink = MeasureInk(family, text, referenceEm, style);
        if (ink.Width <= 0 || ink.Height <= 0) return;

        int em = MaxEm(ink, box);
        // g 的世界变换含 size/32 缩放，换算出字号的物理像素值再决定字重；
        // 降为 Regular 后墨迹更窄，按新字重重测并重取最大字号（字重只判一次，不回切）
        if (ChooseFontStyle(em * g.Transform.Elements[0]) != style)
        {
            style = FontStyle.Regular;
            ink = MeasureInk(family, text, referenceEm, style);
            em = MaxEm(ink, box);
        }

        float k = em / referenceEm;
        var emInk = new RectangleF(ink.X * k, ink.Y * k, ink.Width * k, ink.Height * k);

        using var font = new Font(family, em, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        // 版式原点（DrawString 起点）不等于墨迹左上角，按墨迹框偏移才能做到视觉居中
        g.DrawString(text, font, brush,
            box.Left + (box.Width - emInk.Width) / 2f - emInk.Left,
            box.Top + (box.Height - emInk.Height) / 2f - emInk.Top,
            StringFormat.GenericTypographic);
    }

    /// <summary>物理字号 ≥12px 用 Bold（笔画饱满）；更小用 Regular（Bold 会把小字号的字腔糊死）。</summary>
    internal static FontStyle ChooseFontStyle(float physicalEm) =>
        physicalEm >= 12f ? FontStyle.Bold : FontStyle.Regular;

    /// <summary>墨迹框恰好装进 box 的最大整数字号（下限 7，防异常输入退化成看不见的字号）。</summary>
    private static int MaxEm(RectangleF ink, RectangleF box) =>
        Math.Max(7, (int)Math.Floor(100f * Math.Min(box.Width / ink.Width, box.Height / ink.Height)));

    /// <summary>文本在指定字号的精确墨迹外接框（GraphicsPath 无内边距、无 hinting，几何精确）。</summary>
    private static RectangleF MeasureInk(FontFamily family, string text, float em, FontStyle style)
    {
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)style, em, PointF.Empty, StringFormat.GenericTypographic);
        return path.GetBounds();
    }

    private static void DrawGlyph(Graphics g, Color color, string text, float centerX, float centerY, int pixelSize)
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
