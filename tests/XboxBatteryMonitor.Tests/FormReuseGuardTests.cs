namespace XboxBatteryMonitor.Tests;

using System.Windows.Forms;
using XboxBatteryMonitor.UI;
using Xunit;

public class FormReuseGuardTests
{
    [Fact]
    public void Current_WhenNothingRegistered_IsNull()
    {
        var guard = new FormReuseGuard();
        Assert.Null(guard.Current);
    }

    [Fact]
    public void Register_Form_BecomesCurrent()
    {
        var guard = new FormReuseGuard();
        using var form = new Form();
        guard.Register(form);
        Assert.Same(form, guard.Current);
    }

    [Fact]
    public void Release_RegisteredForm_ClearsCurrent()
    {
        var guard = new FormReuseGuard();
        using var form = new Form();
        guard.Register(form);
        guard.Release(form);
        Assert.Null(guard.Current);
    }

    [Fact]
    public void Release_DifferentForm_DoesNotClearCurrent()
    {
        var guard = new FormReuseGuard();
        using var first = new Form();
        using var second = new Form();
        guard.Register(first);
        guard.Release(second);
        Assert.Same(first, guard.Current);
    }

    [Fact]
    public void Current_DisposedRegisteredForm_IsTreatedAsClosed()
    {
        var guard = new FormReuseGuard();
        var form = new Form();
        guard.Register(form);
        form.Dispose();   // 异常路径：窗体已销毁但未 Release，不得卡死在"已打开"
        Assert.Null(guard.Current);
    }
}
