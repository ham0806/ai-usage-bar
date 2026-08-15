from __future__ import annotations

import time
import tkinter as tk

from ai_usage_bar.models import ProviderSnapshot
from ai_usage_bar.theme import current_theme
from ai_usage_bar.win32_util import apply_tool_window_style, tk_hwnd


class Flyout(tk.Toplevel):
    def __init__(self, master: tk.Misc, anchor: tk.Toplevel) -> None:
        super().__init__(master)
        self._anchor = anchor
        self.overrideredirect(True)
        self.attributes("-topmost", True)
        self.resizable(False, False)
        try:
            self.attributes("-toolwindow", True)
        except tk.TclError:
            pass
        self._body = tk.Frame(self, padx=12, pady=10)
        self._body.pack(fill="both", expand=True)
        self._opened_at = 0.0
        self.bind("<Escape>", lambda _e: self.hide())
        self.bind("<FocusOut>", self._on_focus_out)
        self.withdraw()
        self.after(50, self._style)

    def _style(self) -> None:
        try:
            apply_tool_window_style(tk_hwnd(self), no_activate=False)
        except Exception:
            pass

    def _on_focus_out(self, _event: tk.Event | None = None) -> None:
        self.after(150, self._hide_if_unfocused)

    def _hide_if_unfocused(self) -> None:
        if not self.winfo_exists():
            return
        if time.monotonic() - self._opened_at < 0.4:
            return
        focused = self.focus_get()
        if focused is None:
            self.hide()

    def hide(self) -> None:
        if self.winfo_exists():
            self.withdraw()

    def toggle(self, snapshots: list[ProviderSnapshot]) -> None:
        if self.winfo_ismapped():
            self.hide()
            return
        self.show(snapshots)

    def show(self, snapshots: list[ProviderSnapshot]) -> None:
        theme = current_theme()
        self.configure(bg=theme.border)
        self._body.configure(bg=theme.flyout_bg)
        for child in self._body.winfo_children():
            child.destroy()

        if not snapshots:
            tk.Label(
                self._body,
                text="表示するプロバイダがありません",
                bg=theme.flyout_bg,
                fg=theme.muted,
                font=("Yu Gothic UI", 9),
                anchor="w",
            ).pack(fill="x")
        for index, snapshot in enumerate(snapshots):
            if index:
                tk.Frame(self._body, bg=theme.border, height=1).pack(fill="x", pady=8)
            for line_index, line in enumerate(snapshot.lines):
                fg = theme.fg if line_index == 0 else theme.muted
                if snapshot.error and line_index > 0:
                    fg = theme.warn
                tk.Label(
                    self._body,
                    text=line,
                    bg=theme.flyout_bg,
                    fg=fg,
                    font=("Yu Gothic UI", 9 if line_index else 10),
                    anchor="w",
                    justify="left",
                ).pack(fill="x")

        self.update_idletasks()
        width = max(self._body.winfo_reqwidth() + 8, 260)
        height = self._body.winfo_reqheight() + 8
        self._anchor.update_idletasks()
        ax = self._anchor.winfo_rootx()
        ay = self._anchor.winfo_rooty()
        aw = self._anchor.winfo_width()
        screen_h = self.winfo_screenheight()
        x = ax + aw - width
        y = ay - height - 8
        if y < 8:
            y = ay + self._anchor.winfo_height() + 8
        if y + height > screen_h - 8:
            y = max(8, screen_h - height - 8)
        x = max(8, x)
        self.geometry(f"{width}x{height}+{x}+{y}")
        self._opened_at = time.monotonic()
        self.deiconify()
        self.lift()
        self.focus_force()
