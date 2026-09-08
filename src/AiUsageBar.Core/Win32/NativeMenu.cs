namespace AiUsageBar.Core.Win32;

public static class NativeMenu
{
    public static int Show(nint hwnd, int x, int y, IReadOnlyList<(int Id, string Label)?> items)
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            foreach (var item in items)
            {
                if (item is null)
                {
                    NativeMethods.AppendMenuW(menu, NativeMethods.MfSeparator, 0, "");
                    continue;
                }

                NativeMethods.AppendMenuW(menu, NativeMethods.MfString, (nuint)item.Value.Id, item.Value.Label);
            }

            try
            {
                NativeMethods.SetForegroundWindow(hwnd);
            }
            catch (Exception)
            {
            }

            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TpmLeftAlign | NativeMethods.TpmRightButton | NativeMethods.TpmReturnCmd,
                x,
                y,
                0,
                hwnd,
                0);
            NativeMethods.PostMessageW(hwnd, NativeMethods.WmNull, 0, 0);
            return (int)command;
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }
}
