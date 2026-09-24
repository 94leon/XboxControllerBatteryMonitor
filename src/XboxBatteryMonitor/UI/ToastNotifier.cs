namespace XboxBatteryMonitor.UI;

using Microsoft.Toolkit.Uwp.Notifications;
using XboxBatteryMonitor.Core;

/// <summary>Windows Toast。ToastNotificationManagerCompat 自动为未打包应用处理 AUMID/快捷方式。</summary>
public static class ToastNotifier
{
    public static void Show(string title, string body)
    {
        try
        {
            new ToastContentBuilder().AddText(title).AddText(body).Show();
        }
        catch (Exception ex)
        {
            Logger.Error("Toast 发送失败: " + ex.Message);
        }
    }
}
