from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

from ai_usage_bar.auth import pick_str
from ai_usage_bar.models import jwt_payload

log = logging.getLogger(__name__)

CODEX_CLIENT_ID = "app_EMoamEEZ73f0CkXaXp7hrann"
TOKEN_URL = "https://auth.openai.com/oauth/token"


def auth_json_path() -> Path:
    import os

    home = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))
    return home / "auth.json"


def load_codex_tokens() -> dict[str, str]:
    path = auth_json_path()
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        log.warning("Codex の auth.json を読めませんでした")
        return {}
    if not isinstance(data, dict):
        return {}

    nested = data.get("tokens")
    source: dict[str, Any] = nested if isinstance(nested, dict) else data
    access = pick_str(source, "access_token", "accessToken")
    refresh = pick_str(source, "refresh_token", "refreshToken")
    account = pick_str(source, "account_id", "accountId")
    if not account and access:
        payload = jwt_payload(access)
        auth_claim = payload.get("https://api.openai.com/auth")
        if isinstance(auth_claim, dict):
            account = pick_str(auth_claim, "chatgpt_account_id", "account_id")
        if not account:
            account = pick_str(payload, "chatgpt_account_id", "account_id")
    result: dict[str, str] = {}
    if access:
        result["access_token"] = access
    if refresh:
        result["refresh_token"] = refresh
    if account:
        result["account_id"] = account
    return result


def refresh_access_token(refresh_token: str) -> str | None:
    import httpx

    try:
        response = httpx.post(
            TOKEN_URL,
            data={
                "grant_type": "refresh_token",
                "client_id": CODEX_CLIENT_ID,
                "refresh_token": refresh_token,
                "scope": "openid profile email",
            },
            headers={"Accept": "application/json"},
            timeout=20.0,
        )
    except httpx.HTTPError:
        log.warning("Codex のトークン更新リクエストに失敗しました")
        return None
    if response.status_code >= 400:
        log.warning("Codex のトークン更新が拒否されました status=%s", response.status_code)
        return None
    try:
        payload = response.json()
    except ValueError:
        return None
    if not isinstance(payload, dict):
        return None
    token = pick_str(payload, "access_token")
    return token
