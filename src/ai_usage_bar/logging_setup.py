from __future__ import annotations

import logging
from logging.handlers import RotatingFileHandler

from ai_usage_bar.config import log_path


def setup_logging() -> None:
    path = log_path()
    handler = RotatingFileHandler(
        path,
        maxBytes=1_000_000,
        backupCount=2,
        encoding="utf-8",
    )
    formatter = logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
    handler.setFormatter(formatter)
    root = logging.getLogger()
    root.setLevel(logging.INFO)
    if not any(isinstance(item, RotatingFileHandler) for item in root.handlers):
        root.addHandler(handler)
    logging.getLogger("httpx").setLevel(logging.WARNING)
    logging.getLogger("httpcore").setLevel(logging.WARNING)
