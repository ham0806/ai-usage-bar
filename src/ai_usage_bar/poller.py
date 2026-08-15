from __future__ import annotations

import logging
import threading
import time
from collections.abc import Callable

from ai_usage_bar.config import AppConfig
from ai_usage_bar.models import ProviderSnapshot
from ai_usage_bar.providers.codex import fetch_codex
from ai_usage_bar.providers.cursor import fetch_cursor

log = logging.getLogger(__name__)


class Poller:
    def __init__(
        self,
        get_config: Callable[[], AppConfig],
        on_update: Callable[[list[ProviderSnapshot]], None],
    ) -> None:
        self._get_config = get_config
        self._on_update = on_update
        self._stop = threading.Event()
        self._wake = threading.Event()
        self._thread: threading.Thread | None = None
        self._next_ok: dict[str, float] = {"cursor": 0.0, "codex": 0.0}
        self._last: dict[str, ProviderSnapshot] = {}

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            return
        self._stop.clear()
        self._thread = threading.Thread(target=self._loop, name="ai-usage-bar-poller", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self._wake.set()

    def refresh_now(self) -> None:
        self._next_ok["cursor"] = 0.0
        self._next_ok["codex"] = 0.0
        self._wake.set()

    def _loop(self) -> None:
        while not self._stop.is_set():
            try:
                snapshots = self._collect()
                self._on_update(snapshots)
            except Exception:
                log.exception("使用量の更新処理でエラー")
            config = self._get_config()
            self._wake.wait(timeout=max(30, config.refresh_seconds))
            self._wake.clear()

    def _collect(self) -> list[ProviderSnapshot]:
        config = self._get_config()
        now = time.monotonic()
        snapshots: list[ProviderSnapshot] = []
        if config.show_cursor:
            snapshots.append(self._fetch_one("cursor", now, fetch_cursor))
        if config.show_codex:
            snapshots.append(self._fetch_one("codex", now, fetch_codex))
        return snapshots

    def _fetch_one(
        self,
        key: str,
        now: float,
        fetcher: Callable[[], ProviderSnapshot],
    ) -> ProviderSnapshot:
        cached = self._last.get(key)
        if now < self._next_ok[key] and cached is not None:
            return cached
        snapshot = fetcher()
        self._last[key] = snapshot
        delay = 0.0
        if snapshot.error == "レート制限":
            delay = 300.0
        elif snapshot.error == "要認証":
            delay = 180.0
        self._next_ok[key] = time.monotonic() + delay
        return snapshot
