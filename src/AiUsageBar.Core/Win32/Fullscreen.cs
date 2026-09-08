namespace AiUsageBar.Core.Win32;

public static class Fullscreen
{
    public const int QunsBusy = 2;
    public const int QunsRunningD3dFullScreen = 3;
    public const int QunsPresentationMode = 4;
    public const int QunsApp = 7;
    public const int CoverTolerance = 8;

    private static readonly HashSet<int> YieldNotificationStates =
    [
        QunsRunningD3dFullScreen,
        QunsPresentationMode,
    ];

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "XamlExplorerHostIslandWindow",
        "Windows.Internal.Shell.TabProxyWindow",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "ImmersiveLauncher",
        "TopLevelWindowForOverflowXamlIsland",
    };

    public static bool IsShellWindowClass(string name) => ShellWindowClasses.Contains(name);

    public static bool NotificationStateForcesYield(int? state) =>
        state is int value && YieldNotificationStates.Contains(value);

    public static bool IsFullscreenCover(
        (int Left, int Top, int Right, int Bottom) windowRect,
        (int Left, int Top, int Right, int Bottom) monitorRect,
        int style,
        int tolerance = CoverTolerance)
    {
        if ((style & NativeMethods.WsMinimize) != 0)
        {
            return false;
        }

        if (!RectCoversMonitor(windowRect, monitorRect, tolerance))
        {
            return false;
        }

        if ((style & NativeMethods.WsCaption) != 0 && (style & NativeMethods.WsMaximize) != 0)
        {
            return false;
        }

        return true;
    }

    public static bool NeedsWidgetRestore(bool yielding, bool visible, bool hiddenFlag) =>
        !yielding && (hiddenFlag || !visible);

    public static bool ShouldYieldToFullscreen(TaskbarLayout? layout, nint widgetHwnd)
    {
        try
        {
            if (NotificationStateForcesYield(QueryNotificationState()))
            {
                return true;
            }

            (int Left, int Top, int Right, int Bottom)? monitor = layout is not null
                ? MonitorRectFromRect(layout.Taskbar)
                : widgetHwnd != 0
                    ? MonitorRectFromHwnd(widgetHwnd)
                    : null;
            if (monitor is null)
            {
                return false;
            }

            return ForegroundIsFullscreenOn(monitor.Value, widgetHwnd);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool RectCoversMonitor(
        (int Left, int Top, int Right, int Bottom) windowRect,
        (int Left, int Top, int Right, int Bottom) monitorRect,
        int tolerance)
    {
        return windowRect.Left <= monitorRect.Left + tolerance
            && windowRect.Top <= monitorRect.Top + tolerance
            && windowRect.Right >= monitorRect.Right - tolerance
            && windowRect.Bottom >= monitorRect.Bottom - tolerance;
    }

    private static int? QueryNotificationState()
    {
        var hr = NativeMethods.SHQueryUserNotificationState(out var state);
        return hr == 0 ? state : null;
    }

    private static (int Left, int Top, int Right, int Bottom)? MonitorRectFromHwnd(nint hwnd)
    {
        var handle = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        return MonitorRect(handle);
    }

    private static (int Left, int Top, int Right, int Bottom)? MonitorRectFromRect(
        (int Left, int Top, int Right, int Bottom) rect)
    {
        var point = new POINT { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 };
        var handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
        return MonitorRect(handle);
    }

    private static (int Left, int Top, int Right, int Bottom)? MonitorRect(nint handle)
    {
        if (handle == 0)
        {
            return null;
        }

        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfoW(handle, ref info))
        {
            return null;
        }

        return info.rcMonitor.ToTuple();
    }

    private static bool IsRelatedHwnd(nint hwnd, nint widgetHwnd)
    {
        if (widgetHwnd == 0)
        {
            return false;
        }

        var current = hwnd;
        var seen = new HashSet<nint>();
        while (current != 0 && seen.Add(current))
        {
            if (current == widgetHwnd)
            {
                return true;
            }

            current = NativeMethods.GetWindow(current, NativeMethods.GwOwner);
        }

        return false;
    }

    private static bool ForegroundIsFullscreenOn((int Left, int Top, int Right, int Bottom) monitorRect, nint widgetHwnd)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (IsRelatedHwnd(hwnd, widgetHwnd))
        {
            return false;
        }

        var className = NativeMethods.GetClassName(hwnd);
        if (IsShellWindowClass(className))
        {
            return false;
        }

        var fgMonitor = MonitorRectFromHwnd(hwnd);
        if (fgMonitor != monitorRect)
        {
            return false;
        }

        var style = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle);
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        return IsFullscreenCover(rect.ToTuple(), monitorRect, style);
    }
}
