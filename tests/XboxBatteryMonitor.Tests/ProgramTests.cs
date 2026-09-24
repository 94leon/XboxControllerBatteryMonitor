namespace XboxBatteryMonitor.Tests;

using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using XboxBatteryMonitor.Monitors;
using Xunit;

public class ProgramTests
{
    [Fact]
    public void InstallUiSynchronizationContext_InstallsWinFormsSyncContext()
    {
        // WinForms 同步上下文默认由第一个 Control 的构造安装（晚于 AppContext/BleMonitor 的捕获），
        // 必须显式提前安装，否则所有 _ui.Post 都会进线程池。
        RunOnSta(() =>
        {
            Program.InstallUiSynchronizationContext();
            Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
        });
    }

    [Fact]
    public void BleMonitor_ConstructedAfterInstall_CapturesUiSyncContext()
    {
        // BleMonitor 在字段初始化器里捕获 Current（早于任何构造函数体）。捕获到基类上下文时，
        // 快照事件被 Post 进线程池 → 后台线程清空/重建托盘菜单 → 右键菜单卡死（回归测试）。
        RunOnSta(() =>
        {
            Program.InstallUiSynchronizationContext();
            using var monitor = new BleMonitor();
            FieldInfo? field = typeof(BleMonitor).GetField("_ui", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsType<WindowsFormsSynchronizationContext>(field!.GetValue(monitor));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
