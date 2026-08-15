from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass
from pathlib import Path


APP_NAME = "ai-usage-bar"


def config_dir() -> Path:
    local = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))
    path = local / APP_NAME
    path.mkdir(parents=True, exist_ok=True)
    return path


def config_path() -> Path:
    return config_dir() / "config.json"


def log_path() -> Path:
    temp = Path(os.environ.get("TEMP", Path.home() / "AppData" / "Local" / "Temp"))
    return temp / "ai-usage-bar.log"


@dataclass
class AppConfig:
    refresh_seconds: int = 90
    show_cursor: bool = True
    show_codex: bool = True
    start_with_windows: bool = False
    offset_x: int = 0
    offset_y: int = 0

    def normalized(self) -> AppConfig:
        refresh = int(self.refresh_seconds)
        refresh = max(30, min(3600, refresh))
        return AppConfig(
            refresh_seconds=refresh,
            show_cursor=bool(self.show_cursor),
            show_codex=bool(self.show_codex),
            start_with_windows=bool(self.start_with_windows),
            offset_x=int(self.offset_x),
            offset_y=int(self.offset_y),
        )


def load_config() -> AppConfig:
    path = config_path()
    if not path.exists():
        return AppConfig()
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return AppConfig()
    if not isinstance(data, dict):
        return AppConfig()
    cfg = AppConfig(
        refresh_seconds=data.get("refresh_seconds", 90),
        show_cursor=data.get("show_cursor", True),
        show_codex=data.get("show_codex", True),
        start_with_windows=data.get("start_with_windows", False),
        offset_x=data.get("offset_x", 0),
        offset_y=data.get("offset_y", 0),
    )
    return cfg.normalized()


def save_config(config: AppConfig) -> None:
    path = config_path()
    path.write_text(
        json.dumps(asdict(config.normalized()), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
