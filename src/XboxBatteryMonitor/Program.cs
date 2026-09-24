namespace XboxBatteryMonitor;

using System.Threading;
using System.Windows.Forms;
using XboxBatteryMonitor.Core;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "XboxBatteryMonitor.SingleInstance", out bool createdNew);
        if (!createdNew) return;   // 已有实例 → 静默退出（规格 §8）

        Logger.Info("启动");
        Application.ThreadException += (_, e) => Logger.Error("UI 异常: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("未处理异常: " + (e.ExceptionObject as Exception)?.ToString());

        ConfigureApplication();
        using var context = new AppContext();
        Application.Run(context);
        Logger.Info("退出");
    }

    /// <summary>WinForms 应用级配置。必须在构造任何组件（含 AppContext）之前完成。</summary>
    internal static void ConfigureApplication()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        InstallUiSynchronizationContext();
    }

    /// <summary>
    /// 安装 WinForms UI 同步上下文。默认要到第一个 Control 构造时才安装，晚于
    /// AppContext/BleMonitor 对 Current 的捕获；捕获到基类上下文会把所有 _ui.Post
    /// 送进线程池 → 后台线程重建托盘菜单 → 右键卡死。必须在构造任何组件前调用。
    /// </summary>
    internal static void InstallUiSynchronizationContext() =>
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
}
