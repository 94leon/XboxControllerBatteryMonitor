namespace XboxBatteryMonitor.UI;

using System.Windows.Forms;

/// <summary>单实例窗体守卫：登记当前打开的窗体，阻止重复打开同一窗体。</summary>
internal sealed class FormReuseGuard
{
    private Form? _current;

    /// <summary>已登记且仍存活的窗体；未登记或窗体已销毁时为 null。</summary>
    public Form? Current => _current is { IsDisposed: false } ? _current : null;

    /// <summary>登记窗体为当前打开实例。</summary>
    public void Register(Form form) => _current = form;

    /// <summary>窗体关闭后取消登记；传入其他实例不影响。</summary>
    public void Release(Form form)
    {
        if (ReferenceEquals(_current, form))
            _current = null;
    }
}
