from __future__ import annotations

import ctypes
from ctypes import wintypes
from dataclasses import dataclass

import win32gui

user32 = ctypes.WinDLL("user32", use_last_error=True)
shell32 = ctypes.WinDLL("shell32", use_last_error=True)
shcore = ctypes.WinDLL("shcore", use_last_error=True)

GWL_EXSTYLE = -20
WS_EX_APPWINDOW = 0x00040000
WS_EX_TOOLWINDOW = 0x00000080
WS_EX_NOACTIVATE = 0x08000000
WS_EX_TOPMOST = 0x00000008
HWND_TOPMOST = -1
SWP_NOACTIVATE = 0x0010
SWP_SHOWWINDOW = 0x0040
SWP_NOOWNERZORDER = 0x0200

ABE_LEFT = 0
ABE_TOP = 1
ABE_RIGHT = 2
ABE_BOTTOM = 3
ABM_GETTASKBARPOS = 5


class RECT(ctypes.Structure):
    _fields_ = [
        ("left", ctypes.c_long),
        ("top", ctypes.c_long),
        ("right", ctypes.c_long),
        ("bottom", ctypes.c_long),
    ]


class APPBARDATA(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("hWnd", wintypes.HWND),
        ("uCallbackMessage", wintypes.UINT),
        ("uEdge", wintypes.UINT),
        ("rc", RECT),
        ("lParam", wintypes.LPARAM),
    ]


shell32.SHAppBarMessage.argtypes = [wintypes.DWORD, ctypes.POINTER(APPBARDATA)]
shell32.SHAppBarMessage.restype = ctypes.c_uint
user32.GetDpiForWindow.argtypes = [wintypes.HWND]
user32.GetDpiForWindow.restype = wintypes.UINT


@dataclass(frozen=True)
class TaskbarLayout:
    edge: int
    taskbar: tuple[int, int, int, int]
    tray: tuple[int, int, int, int]
    hwnd: int


def set_dpi_aware() -> None:
    try:
        # DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
        return
    except Exception:
        pass
    try:
        shcore.SetProcessDpiAwareness(2)
        return
    except Exception:
        pass
    try:
        user32.SetProcessDPIAware()
    except Exception:
        pass


def tk_hwnd(widget) -> int:
    widget.update_idletasks()
    hwnd = int(widget.winfo_id())
    parent = user32.GetParent(hwnd)
    return int(parent or hwnd)


def apply_tool_window_style(hwnd: int, *, no_activate: bool = True) -> None:
    style = win32gui.GetWindowLong(hwnd, GWL_EXSTYLE)
    style |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST
    style &= ~WS_EX_APPWINDOW
    if no_activate:
        style |= WS_EX_NOACTIVATE
    win32gui.SetWindowLong(hwnd, GWL_EXSTYLE, style)


def _rect_tuple(rect: RECT | tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    if isinstance(rect, tuple):
        return rect
    return (rect.left, rect.top, rect.right, rect.bottom)


def get_taskbar_layout() -> TaskbarLayout | None:
    tray_hwnd = win32gui.FindWindow("Shell_TrayWnd", None)
    if not tray_hwnd:
        return None

    abd = APPBARDATA()
    abd.cbSize = ctypes.sizeof(APPBARDATA)
    abd.hWnd = tray_hwnd
    shell32.SHAppBarMessage(ABM_GETTASKBARPOS, ctypes.byref(abd))
    taskbar = _rect_tuple(abd.rc)
    if taskbar[2] <= taskbar[0] or taskbar[3] <= taskbar[1]:
        taskbar = win32gui.GetWindowRect(tray_hwnd)

    notify = win32gui.FindWindowEx(tray_hwnd, 0, "TrayNotifyWnd", None)
    if notify:
        tray = win32gui.GetWindowRect(notify)
    else:
        left, top, right, bottom = taskbar
        if abd.uEdge in (ABE_BOTTOM, ABE_TOP):
            width = min(220, max(80, (right - left) // 6))
            tray = (right - width, top, right, bottom)
        else:
            height = min(220, max(80, (bottom - top) // 6))
            tray = (left, bottom - height, right, bottom)

    return TaskbarLayout(edge=int(abd.uEdge), taskbar=taskbar, tray=tray, hwnd=int(tray_hwnd))


def widget_position(
    layout: TaskbarLayout,
    width: int,
    height: int,
    offset_x: int = 0,
    offset_y: int = 0,
    gap: int = 8,
) -> tuple[int, int]:
    tb_l, tb_t, tb_r, tb_b = layout.taskbar
    tr_l, tr_t, tr_r, tr_b = layout.tray

    if layout.edge == ABE_BOTTOM:
        x = tr_l - width - gap
        y = tb_t + max(0, ((tb_b - tb_t) - height) // 2)
    elif layout.edge == ABE_TOP:
        x = tr_l - width - gap
        y = tb_t + max(0, ((tb_b - tb_t) - height) // 2)
    elif layout.edge == ABE_RIGHT:
        x = tb_l + max(0, ((tb_r - tb_l) - width) // 2)
        y = tr_t - height - gap
    else:
        x = tb_l + max(0, ((tb_r - tb_l) - width) // 2)
        y = tr_t - height - gap

    x += offset_x
    y += offset_y
    x = min(max(x, tb_l + 4), max(tb_l + 4, tb_r - width - 4))
    y = min(max(y, tb_t + 2), max(tb_t + 2, tb_b - height - 2))
    return x, y


def place_topmost(hwnd: int, x: int, y: int, width: int, height: int) -> None:
    win32gui.SetWindowPos(
        hwnd,
        HWND_TOPMOST,
        x,
        y,
        width,
        height,
        SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER,
    )


def taskbar_thickness(layout: TaskbarLayout) -> int:
    left, top, right, bottom = layout.taskbar
    if layout.edge in (ABE_LEFT, ABE_RIGHT):
        return max(32, right - left)
    return max(32, bottom - top)
