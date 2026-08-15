from __future__ import annotations

import logging
from typing import Any

import keyring
import keyring.errors

log = logging.getLogger(__name__)

SERVICE = "ai-usage-bar"
CURSOR_TOKEN_USER = "cursor-session-token"


def get_cursor_token() -> str | None:
    try:
        value = keyring.get_password(SERVICE, CURSOR_TOKEN_USER)
    except keyring.errors.KeyringError:
        log.warning("Credential Manager から Cursor トークンを読めませんでした")
        return None
    if not value:
        return None
    text = value.strip()
    return text or None


def set_cursor_token(token: str | None) -> None:
    try:
        if not token:
            try:
                keyring.delete_password(SERVICE, CURSOR_TOKEN_USER)
            except keyring.errors.PasswordDeleteError:
                return
            return
        keyring.set_password(SERVICE, CURSOR_TOKEN_USER, token.strip())
    except keyring.errors.KeyringError:
        log.warning("Credential Manager へ Cursor トークンを保存できませんでした")


def as_safe_error(exc: BaseException) -> str:
    text = str(exc).strip() or exc.__class__.__name__
    lowered = text.lower()
    for secret in ("bearer ", "cookie", "eyj", "workoscursor"):
        if secret in lowered:
            return exc.__class__.__name__
    if len(text) > 180:
        return text[:177] + "..."
    return text


def pick_str(data: dict[str, Any], *keys: str) -> str | None:
    for key in keys:
        value = data.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return None
