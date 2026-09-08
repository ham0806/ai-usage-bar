namespace AiUsageBar.Core.Win32;

public static class WindowChrome
{
    public static void SetDpiAware() => NativeMethods.SetDpiAware();

    public static void MessageBox(string text, string caption) => NativeMethods.MessageBoxW(0, text, caption, 0);

    public static bool TryGetWindowRect(nint hwnd, out (int Left, int Top, int Right, int Bottom) rect)
    {
        if (NativeMethods.GetWindowRect(hwnd, out var native))
        {
            rect = native.ToTuple();
            return true;
        }

        rect = default;
        return false;
    }

    public static void ApplyToolWindowStyle(nint hwnd, bool noActivate = true)
    {
        var style = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
        style |= NativeMethods.WsExToolWindow | NativeMethods.WsExTopmost;
        style &= ~NativeMethods.WsExAppWindow;
        if (noActivate)
        {
            style |= NativeMethods.WsExNoActivate;
        }

        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, style);
    }

    public static void ApplyWin11Surface(nint hwnd, bool dark, bool rounded = true, bool mica = true, string? border = null)
    {
        try
        {
            DwmSet(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, dark ? 1 : 0);
        }
        catch (Exception)
        {
        }

        try
        {
            DwmSet(hwnd, NativeMethods.DwmwaWindowCornerPreference, rounded ? NativeMethods.DwmwcpRound : NativeMethods.DwmwcpDoNotRound);
        }
        catch (Exception)
        {
        }

        if (mica)
        {
            try
            {
                DwmSet(hwnd, NativeMethods.DwmwaSystemBackdropType, NativeMethods.DwmsbtTransientWindow);
            }
            catch (Exception)
            {
                try
                {
                    DwmSet(hwnd, NativeMethods.DwmwaSystemBackdropType, NativeMethods.DwmsbtMainWindow);
                }
                catch (Exception)
                {
                }
            }
        }

        if (border is not null)
        {
            try
            {
                DwmSet(hwnd, NativeMethods.DwmwaBorderColor, unchecked((int)Themes.HexToColorRef(border)));
            }
            catch (Exception)
            {
            }
        }
    }

    public static void PlaceTopmost(nint hwnd, int x, int y, int width, int height)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopmost,
            x,
            y,
            width,
            height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpNoOwnerZOrder);
    }

    public static bool IsWindowShown(nint hwnd)
    {
        if (hwnd == 0)
        {
            return false;
        }

        try
        {
            return NativeMethods.IsWindow(hwnd) && NativeMethods.IsWindowVisible(hwnd);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void SetWindowShown(nint hwnd, bool shown)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        NativeMethods.ShowWindow(hwnd, shown ? NativeMethods.SwShowNoActivate : NativeMethods.SwHide);
    }

    private static void DwmSet(nint hwnd, int attribute, int value)
    {
        var data = value;
        NativeMethods.DwmSetWindowAttribute(hwnd, attribute, ref data, sizeof(int));
    }
}
