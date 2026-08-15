from __future__ import annotations

import logging
import sys
import tkinter as tk
from tkinter import messagebox

from ai_usage_bar.config import AppConfig, load_config, save_config
from ai_usage_bar.flyout import Flyout
from ai_usage_bar.logging_setup import setup_logging
from ai_usage_bar.models import ProviderSnapshot
from ai_usage_bar.poller import Poller
from ai_usage_bar.settings import SettingsWindow
from ai_usage_bar.single_instance import SingleInstance
from ai_usage_bar.startup import set_startup_enabled
from ai_usage_bar.widget import TaskbarWidget
from ai_usage_bar.win32_util import set_dpi_aware

log = logging.getLogger(__name__)


class UsageBarApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.config = load_config()
        self.snapshots: list[ProviderSnapshot] = []
        self._settings: SettingsWindow | None = None
        self.widget = TaskbarWidget(
            root,
            on_left_click=self.toggle_flyout,
            on_refresh=self.refresh_now,
            on_settings=self.open_settings,
            on_exit=self.quit,
            get_offset=lambda: (self.config.offset_x, self.config.offset_y),
        )
        self.flyout = Flyout(root, self.widget)
        self.poller = Poller(lambda: self.config, self._on_snapshots)

    def start(self) -> None:
        if self.config.start_with_windows:
            try:
                set_startup_enabled(True)
            except Exception:
                log.warning("スタートアップ登録の同期に失敗しました")
        self.poller.start()

    def _on_snapshots(self, snapshots: list[ProviderSnapshot]) -> None:
        self.root.after(0, lambda: self._apply_snapshots(snapshots))

    def _apply_snapshots(self, snapshots: list[ProviderSnapshot]) -> None:
        self.snapshots = snapshots
        self.widget.set_snapshots(snapshots)
        if self.flyout.winfo_ismapped():
            self.flyout.show(snapshots)

    def toggle_flyout(self) -> None:
        self.flyout.toggle(self.snapshots)

    def refresh_now(self) -> None:
        self.poller.refresh_now()

    def open_settings(self) -> None:
        if self._settings is not None and self._settings.winfo_exists():
            self._settings.lift()
            self._settings.focus_force()
            return
        self._settings = SettingsWindow(self.root, self.config, self._save_settings)

    def _save_settings(self, config: AppConfig) -> None:
        self.config = config
        save_config(config)
        try:
            set_startup_enabled(config.start_with_windows)
        except Exception:
            log.exception("スタートアップ登録に失敗しました")
            messagebox.showwarning("AI Usage Bar", "スタートアップへの登録または解除に失敗しました。")
        self.refresh_now()
        self.widget.dock()

    def quit(self) -> None:
        self.poller.stop()
        self.root.quit()
        self.root.destroy()


def main() -> int:
    setup_logging()
    instance = SingleInstance()
    if not instance.owned:
        instance.close()
        try:
            import ctypes

            ctypes.windll.user32.MessageBoxW(0, "AI Usage Bar はすでに起動しています。", "AI Usage Bar", 0)
        except Exception:
            pass
        return 0

    set_dpi_aware()
    root = tk.Tk()
    root.withdraw()
    root.title("AI Usage Bar")
    app = UsageBarApp(root)
    root.protocol("WM_DELETE_WINDOW", app.quit)
    try:
        app.start()
        root.mainloop()
    finally:
        instance.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
