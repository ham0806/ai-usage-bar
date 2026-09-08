namespace AiUsageBar.Core.Win32;

public sealed record TaskbarLayout(
    int Edge,
    (int Left, int Top, int Right, int Bottom) Taskbar,
    (int Left, int Top, int Right, int Bottom) Tray,
    nint Hwnd);

public static class Taskbar
{
    public static TaskbarLayout? GetLayout()
    {
        var trayHwnd = NativeMethods.FindWindowW("Shell_TrayWnd", null);
        if (trayHwnd == 0)
        {
            return null;
        }

        var abd = new APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
            hWnd = trayHwnd,
        };
        NativeMethods.SHAppBarMessage(NativeMethods.AbmGetTaskbarPos, ref abd);
        var taskbar = abd.rc.ToTuple();
        if (taskbar.Right <= taskbar.Left || taskbar.Bottom <= taskbar.Top)
        {
            NativeMethods.GetWindowRect(trayHwnd, out var fallback);
            taskbar = fallback.ToTuple();
        }

        (int Left, int Top, int Right, int Bottom) tray;
        var notify = NativeMethods.FindWindowExW(trayHwnd, 0, "TrayNotifyWnd", null);
        if (notify != 0 && NativeMethods.GetWindowRect(notify, out var notifyRect))
        {
            tray = notifyRect.ToTuple();
        }
        else
        {
            var (left, top, right, bottom) = taskbar;
            if (abd.uEdge is NativeMethods.AbeBottom or NativeMethods.AbeTop)
            {
                var width = Math.Min(220, Math.Max(80, (right - left) / 6));
                tray = (right - width, top, right, bottom);
            }
            else
            {
                var height = Math.Min(220, Math.Max(80, (bottom - top) / 6));
                tray = (left, bottom - height, right, bottom);
            }
        }

        return new TaskbarLayout((int)abd.uEdge, taskbar, tray, trayHwnd);
    }

    public static (int X, int Y) WidgetPosition(
        TaskbarLayout layout,
        int width,
        int height,
        int offsetX = 0,
        int offsetY = 0,
        int gap = 8)
    {
        var (tbL, tbT, tbR, tbB) = layout.Taskbar;
        var (trL, trT, trR, trB) = layout.Tray;
        int x;
        int y;
        if (layout.Edge is NativeMethods.AbeBottom or NativeMethods.AbeTop)
        {
            x = trL - width - gap;
            y = tbT + Math.Max(0, ((tbB - tbT) - height) / 2);
        }
        else
        {
            x = tbL + Math.Max(0, ((tbR - tbL) - width) / 2);
            y = trT - height - gap;
        }

        x += offsetX;
        y += offsetY;
        x = Math.Min(Math.Max(x, tbL + 4), Math.Max(tbL + 4, tbR - width - 4));
        y = Math.Min(Math.Max(y, tbT + 2), Math.Max(tbT + 2, tbB - height - 2));
        return (x, y);
    }

    public static int Thickness(TaskbarLayout layout)
    {
        var (left, top, right, bottom) = layout.Taskbar;
        return layout.Edge is NativeMethods.AbeLeft or NativeMethods.AbeRight
            ? Math.Max(32, right - left)
            : Math.Max(32, bottom - top);
    }

    public static int MonitorDpi((int Left, int Top, int Right, int Bottom)? rect = null, nint hwnd = 0)
    {
        try
        {
            nint handle = 0;
            if (rect is { } r)
            {
                var point = new POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
                handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
            }
            else if (hwnd != 0)
            {
                handle = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
            }

            if (handle != 0 && NativeMethods.GetDpiForMonitor(handle, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX != 0)
            {
                return (int)dpiX;
            }

            if (hwnd != 0)
            {
                var dpi = (int)NativeMethods.GetDpiForWindow(hwnd);
                if (dpi != 0)
                {
                    return dpi;
                }
            }
        }
        catch (Exception)
        {
        }

        return NativeMethods.BaseDpi;
    }

    public static string? SampleColor(TaskbarLayout layout, (int Left, int Top, int Right, int Bottom)? exclude = null)
    {
        var (tbL, tbT, tbR, tbB) = layout.Taskbar;
        var (trL, trT, trR, trB) = layout.Tray;
        var points = new List<(int X, int Y)>();
        if (layout.Edge is NativeMethods.AbeLeft or NativeMethods.AbeRight)
        {
            var cx = (tbL + tbR) / 2;
            for (var dy = 24; dy < 360; dy += 10)
            {
                points.Add((cx, trT - dy));
                points.Add((cx, trB + dy));
            }

            if (exclude is { } ex)
            {
                points.Add((cx, ex.Top - 12));
                points.Add((cx, ex.Bottom + 12));
            }
        }
        else
        {
            var cy = (tbT + tbB) / 2;
            for (var dx = 24; dx < 360; dx += 10)
            {
                points.Add((trL - dx, cy));
                points.Add((trR + dx, cy));
            }

            if (exclude is { } ex)
            {
                points.Add((ex.Left - 12, cy));
                points.Add((ex.Right + 12, cy));
            }
        }

        var samples = new List<(int R, int G, int B)>();
        var hdc = NativeMethods.GetDC(0);
        try
        {
            foreach (var (x, y) in points)
            {
                if (!PointInRect(x, y, layout.Taskbar))
                {
                    continue;
                }

                if (exclude is { } ex && PointInRect(x, y, ex, 4))
                {
                    continue;
                }

                var pixel = NativeMethods.GetPixel(hdc, x, y);
                if (pixel == 0xFFFFFFFF)
                {
                    continue;
                }

                samples.Add(((int)(pixel & 0xFF), (int)((pixel >> 8) & 0xFF), (int)((pixel >> 16) & 0xFF)));
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(0, hdc);
        }

        if (samples.Count == 0)
        {
            return null;
        }

        var median = MedianRgb(samples);
        var clustered = samples.Where(item => NearRgb(item, median, 18)).ToList();
        if (clustered.Count > 0)
        {
            median = MedianRgb(clustered);
        }

        return $"#{median.R:x2}{median.G:x2}{median.B:x2}";
    }

    private static bool PointInRect(int x, int y, (int Left, int Top, int Right, int Bottom) rect, int pad = 0) =>
        x >= rect.Left - pad && x <= rect.Right + pad && y >= rect.Top - pad && y <= rect.Bottom + pad;

    private static int MedianChannel(IReadOnlyList<int> values)
    {
        var ordered = values.OrderBy(v => v).ToList();
        return ordered[ordered.Count / 2];
    }

    private static (int R, int G, int B) MedianRgb(IReadOnlyList<(int R, int G, int B)> samples) =>
        (MedianChannel(samples.Select(s => s.R).ToList()), MedianChannel(samples.Select(s => s.G).ToList()), MedianChannel(samples.Select(s => s.B).ToList()));

    private static bool NearRgb((int R, int G, int B) left, (int R, int G, int B) right, int tolerance) =>
        Math.Max(Math.Abs(left.R - right.R), Math.Max(Math.Abs(left.G - right.G), Math.Abs(left.B - right.B))) <= tolerance;
}
