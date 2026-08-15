from __future__ import annotations

import tkinter as tk
from collections.abc import Callable
from tkinter import ttk

from ai_usage_bar.auth import get_cursor_token, set_cursor_token
from ai_usage_bar.config import AppConfig
from ai_usage_bar.theme import current_theme


class SettingsWindow(tk.Toplevel):
    def __init__(
        self,
        master: tk.Misc,
        config: AppConfig,
        on_save: Callable[[AppConfig], None],
    ) -> None:
        super().__init__(master)
        self._on_save = on_save
        self.title("AI Usage Bar の設定")
        self.resizable(False, False)
        self.attributes("-topmost", True)
        theme = current_theme()
        self.configure(bg=theme.flyout_bg)

        pad = {"padx": 12, "pady": 6}
        frame = tk.Frame(self, bg=theme.flyout_bg)
        frame.pack(fill="both", expand=True, padx=8, pady=8)

        self.refresh_var = tk.StringVar(value=str(config.refresh_seconds))
        self.show_cursor_var = tk.BooleanVar(value=config.show_cursor)
        self.show_codex_var = tk.BooleanVar(value=config.show_codex)
        self.startup_var = tk.BooleanVar(value=config.start_with_windows)
        self.offset_x_var = tk.StringVar(value=str(config.offset_x))
        self.offset_y_var = tk.StringVar(value=str(config.offset_y))
        saved = get_cursor_token()
        self.token_var = tk.StringVar(value=saved or "")

        tk.Label(frame, text="更新間隔（秒）", bg=theme.flyout_bg, fg=theme.fg).grid(row=0, column=0, sticky="w", **pad)
        ttk.Entry(frame, textvariable=self.refresh_var, width=12).grid(row=0, column=1, sticky="w", **pad)

        ttk.Checkbutton(frame, text="Cursor を表示", variable=self.show_cursor_var).grid(
            row=1, column=0, columnspan=2, sticky="w", **pad
        )
        ttk.Checkbutton(frame, text="Codex を表示", variable=self.show_codex_var).grid(
            row=2, column=0, columnspan=2, sticky="w", **pad
        )
        ttk.Checkbutton(frame, text="Windows 起動時に開始", variable=self.startup_var).grid(
            row=3, column=0, columnspan=2, sticky="w", **pad
        )

        tk.Label(frame, text="位置オフセット X", bg=theme.flyout_bg, fg=theme.fg).grid(row=4, column=0, sticky="w", **pad)
        ttk.Entry(frame, textvariable=self.offset_x_var, width=12).grid(row=4, column=1, sticky="w", **pad)
        tk.Label(frame, text="位置オフセット Y", bg=theme.flyout_bg, fg=theme.fg).grid(row=5, column=0, sticky="w", **pad)
        ttk.Entry(frame, textvariable=self.offset_y_var, width=12).grid(row=5, column=1, sticky="w", **pad)

        tk.Label(
            frame,
            text="Cursor セッショントークン（任意）",
            bg=theme.flyout_bg,
            fg=theme.fg,
        ).grid(row=6, column=0, columnspan=2, sticky="w", **pad)
        ttk.Entry(frame, textvariable=self.token_var, width=48, show="•").grid(
            row=7, column=0, columnspan=2, sticky="we", **pad
        )
        tk.Label(
            frame,
            text="cursor.com の Cookie「WorkosCursorSessionToken」を貼り付けます。ローカルの Cursor から取れる場合は空で構いません。",
            bg=theme.flyout_bg,
            fg=theme.muted,
            wraplength=420,
            justify="left",
        ).grid(row=8, column=0, columnspan=2, sticky="w", **pad)

        buttons = tk.Frame(frame, bg=theme.flyout_bg)
        buttons.grid(row=9, column=0, columnspan=2, sticky="e", **pad)
        ttk.Button(buttons, text="保存したトークンを削除", command=self._clear_token).pack(side="left", padx=4)
        ttk.Button(buttons, text="キャンセル", command=self.destroy).pack(side="left", padx=4)
        ttk.Button(buttons, text="保存", command=self._save).pack(side="left", padx=4)

        self.transient(master.winfo_toplevel())
        self.grab_set()
        self.focus_force()

    def _clear_token(self) -> None:
        self.token_var.set("")
        set_cursor_token(None)

    def _save(self) -> None:
        def as_int(raw: str, fallback: int) -> int:
            try:
                return int(raw.strip())
            except ValueError:
                return fallback

        config = AppConfig(
            refresh_seconds=as_int(self.refresh_var.get(), 90),
            show_cursor=self.show_cursor_var.get(),
            show_codex=self.show_codex_var.get(),
            start_with_windows=self.startup_var.get(),
            offset_x=as_int(self.offset_x_var.get(), 0),
            offset_y=as_int(self.offset_y_var.get(), 0),
        ).normalized()
        token = self.token_var.get().strip()
        set_cursor_token(token or None)
        self._on_save(config)
        self.destroy()
