from __future__ import annotations

import tkinter as tk
from collections.abc import Callable

from ai_usage_bar.models import ProviderSnapshot, compose_compact
from ai_usage_bar.theme import Theme, current_theme, percent_color
from ai_usage_bar.win32_util import (
    apply_tool_window_style,
    get_taskbar_layout,
    place_topmost,
    taskbar_thickness,
    tk_hwnd,
    widget_position,
)


class TaskbarWidget(tk.Toplevel):
    def __init__(
        self,
        master: tk.Misc,
        *,
        on_left_click: Callable[[], None],
        on_refresh: Callable[[], None],
        on_settings: Callable[[], None],
        on_exit: Callable[[], None],
        get_offset: Callable[[], tuple[int, int]],
    ) -> None:
        super().__init__(master)
        self._on_left_click = on_left_click
        self._on_refresh = on_refresh
        self._on_settings = on_settings
        self._on_exit = on_exit
        self._get_offset = get_offset
        self._theme: Theme = current_theme()
        self._snapshots: list[ProviderSnapshot] = []
        self._hwnd: int | None = None
        self._styled = False

        self.overrideredirect(True)
        self.attributes("-topmost", True)
        self.resizable(False, False)
        try:
            self.attributes("-toolwindow", True)
        except tk.TclError:
            pass

        self._frame = tk.Frame(self, bd=0, highlightthickness=1)
        self._frame.pack(fill="both", expand=True)
        self._label = tk.Label(self._frame, text="AI Usage Bar", padx=10, pady=2, font=("Yu Gothic UI", 9))
        self._label.pack(fill="both", expand=True)

        for widget in (self, self._frame, self._label):
            widget.bind("<Button-1>", self._left_click)
            widget.bind("<Button-3>", self._right_click)

        self._menu = tk.Menu(self, tearoff=0)
        self._apply_theme()
        self.after(50, self._init_hwnd)
        self.after(200, self._tick)

    def set_snapshots(self, snapshots: list[ProviderSnapshot]) -> None:
        self._snapshots = snapshots
        self._label.configure(text=compose_compact(snapshots) or "AI Usage Bar")
        self._apply_theme()
        self._fit()
        self.dock()

    def _label_color(self) -> str:
        percents = [item.used_percent for item in self._snapshots if item.ok and item.used_percent is not None]
        if percents:
            return percent_color(self._theme, max(percents))
        if any(not item.ok for item in self._snapshots):
            return self._theme.warn
        return self._theme.fg

    def _init_hwnd(self) -> None:
        try:
            self._hwnd = tk_hwnd(self)
            apply_tool_window_style(self._hwnd, no_activate=True)
            self._styled = True
        except Exception:
            self._hwnd = None
        self.dock()

    def _apply_theme(self) -> None:
        self._theme = current_theme()
        bg = self._theme.bg
        self.configure(bg=bg)
        self._frame.configure(bg=bg, highlightbackground=self._theme.border, highlightcolor=self._theme.border)
        self._label.configure(bg=bg, fg=self._label_color())
        self._menu.configure(
            bg=self._theme.flyout_bg,
            fg=self._theme.fg,
            activebackground=self._theme.accent,
            activeforeground=self._theme.bg,
        )

    def _fit(self) -> None:
        self.update_idletasks()
        layout = get_taskbar_layout()
        thickness = taskbar_thickness(layout) if layout else 48
        height = max(28, min(36, thickness - 8))
        req_w = max(self._label.winfo_reqwidth() + 8, 160)
        width = min(520, req_w)
        self.geometry(f"{width}x{height}")

    def dock(self) -> None:
        layout = get_taskbar_layout()
        if layout is None:
            return
        self._fit()
        self.update_idletasks()
        width = max(self.winfo_width(), 160)
        height = max(self.winfo_height(), 28)
        ox, oy = self._get_offset()
        x, y = widget_position(layout, width, height, ox, oy)
        hwnd = self._hwnd or tk_hwnd(self)
        self._hwnd = hwnd
        if not self._styled:
            try:
                apply_tool_window_style(hwnd, no_activate=True)
                self._styled = True
            except Exception:
                pass
        try:
            place_topmost(hwnd, x, y, width, height)
        except Exception:
            self.geometry(f"{width}x{height}+{x}+{y}")

    def _tick(self) -> None:
        if not self.winfo_exists():
            return
        self._apply_theme()
        self.dock()
        self.after(400, self._tick)

    def _left_click(self, _event: tk.Event | None = None) -> str:
        self._on_left_click()
        return "break"

    def _right_click(self, event: tk.Event) -> str:
        self._menu.delete(0, "end")
        self._menu.add_command(label="今すぐ更新", command=self._on_refresh)
        self._menu.add_command(label="設定", command=self._on_settings)
        self._menu.add_separator()
        self._menu.add_command(label="終了", command=self._on_exit)
        try:
            self._menu.tk_popup(event.x_root, event.y_root)
        finally:
            self._menu.grab_release()
        return "break"
