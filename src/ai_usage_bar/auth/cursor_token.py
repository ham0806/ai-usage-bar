from __future__ import annotations

import logging
import os
import shutil
import sqlite3
import tempfile
from pathlib import Path
from urllib.parse import unquote

from ai_usage_bar.auth import get_cursor_token
from ai_usage_bar.models import jwt_payload

log = logging.getLogger(__name__)

ACCESS_TOKEN_KEYS = (
    "cursorAuth/accessToken",
    "cursorAuth/cachedAccessToken",
)


def state_db_path() -> Path:
    appdata = Path(os.environ.get("APPDATA", Path.home() / "AppData" / "Roaming"))
    return appdata / "Cursor" / "User" / "globalStorage" / "state.vscdb"


def _read_item(db_path: Path, key: str) -> str | None:
    tmp_path: str | None = None
    try:
        with tempfile.NamedTemporaryFile(prefix="ai-usage-bar-", suffix=".vscdb", delete=False) as tmp:
            tmp_path = tmp.name
        shutil.copy2(db_path, tmp_path)
        conn = sqlite3.connect(tmp_path)
        try:
            row = conn.execute("SELECT value FROM ItemTable WHERE key = ?", (key,)).fetchone()
        finally:
            conn.close()
    except (OSError, sqlite3.Error):
        return None
    finally:
        if tmp_path:
            Path(tmp_path).unlink(missing_ok=True)
    if not row or row[0] is None:
        return None
    value = row[0]
    if isinstance(value, bytes):
        try:
            value = value.decode("utf-8")
        except UnicodeDecodeError:
            return None
    text = str(value).strip()
    if text.startswith('"') and text.endswith('"'):
        text = text[1:-1]
    return text or None


def _looks_like_jwt(token: str) -> bool:
    return token.startswith("eyJ") and token.count(".") >= 2


def normalize_session_cookie(raw: str) -> str | None:
    text = raw.strip().strip('"')
    if not text:
        return None
    if text.lower().startswith("workoscursorsessiontoken="):
        text = text.split("=", 1)[1].strip()
    text = unquote(text)
    if "::" in text:
        left, right = text.split("::", 1)
        if _looks_like_jwt(right):
            return f"{left}%3A%3A{right}"
        return text.replace("::", "%3A%3A", 1)
    if "%3A%3A" in text.upper():
        return text
    if _looks_like_jwt(text):
        payload = jwt_payload(text)
        sub = payload.get("sub")
        if isinstance(sub, str) and sub:
            return f"{sub}%3A%3A{text}"
        return text
    return text


def load_cursor_cookie(*, prefer_saved: bool = False) -> str | None:
    saved = get_cursor_token()
    db_token: str | None = None
    db_path = state_db_path()
    if db_path.is_file():
        for key in ACCESS_TOKEN_KEYS:
            raw = _read_item(db_path, key)
            if raw:
                db_token = raw
                break
        if db_token is None:
            log.info("Cursor の state.vscdb に accessToken がありませんでした")
    else:
        log.info("Cursor の state.vscdb が見つかりません")

    if prefer_saved:
        candidates = (saved, db_token)
    else:
        candidates = (db_token, saved)

    for raw in candidates:
        if not raw:
            continue
        cookie = normalize_session_cookie(raw)
        if cookie:
            return cookie
    return None
