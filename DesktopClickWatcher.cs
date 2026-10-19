using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace Corral;

/// <summary>
/// Watches for a double-click on empty desktop space (not on an icon) using a low-level mouse hook.
/// The hook callback only does cheap checks; the icon-vs-empty test runs off-thread via UI Automation.
/// </summary>
internal sealed class DesktopClickWatcher : IDisposable
{
    private readonly Func<bool> _enabled;
    private readonly Action<bool> _onDoubleClick;
    private readonly Native.LowLevelMouseProc _proc;   // keep a reference so the delegate isn't collected
    private IntPtr _hook;

    private bool _lastOnDesktop;
    private uint _lastTime;
    private POINT _lastPoint;

    /// <param name="onDoubleClick">Called on the UI thread with true when Shift was held for the double-click.</param>
    public DesktopClickWatcher(Func<bool> enabled, Action<bool> onDoubleClick)
    {
        _enabled = enabled;
        _onDoubleClick = onDoubleClick;
        _proc = HookProc;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Write("Could not install mouse hook");
    }

    private IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && wParam == (IntPtr)Native.WM_LBUTTONDOWN && _enabled())
                OnLeftDown(Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam));
        }
        catch (Exception ex)
        {
            Log.Write("Mouse hook error", ex);
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void OnLeftDown(Native.MSLLHOOKSTRUCT e)
    {
        if (!Native.IsDesktopSurface(e.pt))
        {
            _lastOnDesktop = false;
            return;
        }

        int slopX = Math.Max(2, Native.GetSystemMetrics(36 /* SM_CXDOUBLECLK */) / 2);
        int slopY = Math.Max(2, Native.GetSystemMetrics(37 /* SM_CYDOUBLECLK */) / 2);
        bool isDouble = _lastOnDesktop
            && unchecked(e.time - _lastTime) <= Native.GetDoubleClickTime()
            && Math.Abs(e.pt.X - _lastPoint.X) <= slopX
            && Math.Abs(e.pt.Y - _lastPoint.Y) <= slopY;

        if (!isDouble)
        {
            _lastOnDesktop = true;
            _lastTime = e.time;
            _lastPoint = e.pt;
            return;
        }

        _lastOnDesktop = false;
        POINT pt = e.pt;
        bool shift = Native.IsShiftDown(); // read now, in the hook, while it still reflects this click
        Task.Run(() =>
        {
            if (IsOverIcon(pt)) return;
            Application.Current?.Dispatcher.BeginInvoke(() => _onDoubleClick(shift));
        });
    }

    /// <summary>Asks UI Automation what is under the cursor; any failure counts as "an icon" so we never act by mistake.</summary>
    private static bool IsOverIcon(POINT pt)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
            return element == null || element.Current.ControlType == ControlType.ListItem;
        }
        catch
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
