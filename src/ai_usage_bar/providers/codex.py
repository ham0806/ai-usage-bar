from __future__ import annotations

import json
import logging
import queue
import shutil
import subprocess
import sys
import threading
import time
from datetime import datetime
from typing import Any

import httpx

from ai_usage_bar.auth import as_safe_error
from ai_usage_bar.auth.codex_auth import load_codex_tokens, refresh_access_token
from ai_usage_bar.models import (
    ProviderSnapshot,
    RateWindow,
    format_percent,
    format_reset,
    parse_timestamp,
    utcnow,
)
from ai_usage_bar.providers import AuthRequired, FetchError, RateLimited

log = logging.getLogger(__name__)

USAGE_URL = "https://chatgpt.com/backend-api/wham/usage"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)
CREATE_NO_WINDOW = 0x08000000


def fetch_codex() -> ProviderSnapshot:
    updated = utcnow()
    try:
        payload = _fetch_payload()
        snapshot = _snapshot_from_payload(payload, updated)
        return snapshot
    except AuthRequired as exc:
        return ProviderSnapshot(
            provider_id="codex",
            title="Codex",
            ok=False,
            compact="Codex 要認証",
            lines=["Codex", as_safe_error(exc) or "ログイン情報が見つかりません"],
            error="要認証",
            updated_at=updated,
        )
    except RateLimited:
        return ProviderSnapshot(
            provider_id="codex",
            title="Codex",
            ok=False,
            compact="Codex 制限中",
            lines=["Codex", "レート制限中です。しばらく待ってから再取得します"],
            error="レート制限",
            updated_at=updated,
        )
    except FetchError as exc:
        return ProviderSnapshot(
            provider_id="codex",
            title="Codex",
            ok=False,
            compact="Codex --",
            lines=["Codex", as_safe_error(exc)],
            error=as_safe_error(exc),
            updated_at=updated,
        )
    except Exception:
        log.exception("Codex の使用量取得で予期しないエラー")
        return ProviderSnapshot(
            provider_id="codex",
            title="Codex",
            ok=False,
            compact="Codex --",
            lines=["Codex", "取得に失敗しました"],
            error="取得失敗",
            updated_at=updated,
        )


def _fetch_payload() -> dict[str, Any]:
    tokens = load_codex_tokens()
    http_payload: dict[str, Any] | None = None
    http_error: Exception | None = None
    if tokens.get("access_token"):
        try:
            http_payload = _fetch_http(tokens["access_token"], tokens.get("account_id"))
        except AuthRequired as exc:
            if tokens.get("refresh_token"):
                new_token = refresh_access_token(tokens["refresh_token"])
                if new_token:
                    try:
                        http_payload = _fetch_http(new_token, tokens.get("account_id"))
                    except Exception as retry_exc:
                        http_error = retry_exc
                else:
                    http_error = exc
            else:
                http_error = exc
        except (RateLimited, FetchError) as exc:
            http_error = exc
            log.info("Codex HTTP 取得に失敗したので app-server に切り替えます")
    else:
        log.info("Codex の auth.json に access_token がありません")

    if http_payload is not None and _collect_windows(http_payload):
        return http_payload
    if http_payload is not None:
        log.info("Codex HTTP 応答にレート枠が無いので app-server を試します")

    try:
        rpc_payload = _fetch_app_server()
        if _collect_windows(rpc_payload) or http_payload is None:
            return rpc_payload
        return http_payload
    except Exception as exc:
        if http_payload is not None:
            return http_payload
        if isinstance(http_error, (AuthRequired, RateLimited)):
            raise http_error
        if isinstance(exc, (AuthRequired, RateLimited, FetchError)):
            raise
        raise FetchError("Codex の使用量を取得できませんでした") from exc


def _fetch_http(access_token: str, account_id: str | None) -> dict[str, Any]:
    headers = {
        "Authorization": f"Bearer {access_token}",
        "Accept": "application/json",
        "User-Agent": USER_AGENT,
        "Referer": "https://chatgpt.com/",
    }
    if account_id:
        headers["ChatGPT-Account-ID"] = account_id
        headers["chatgpt-account-id"] = account_id
    try:
        response = httpx.get(USAGE_URL, headers=headers, timeout=20.0)
    except httpx.HTTPError as exc:
        raise FetchError("Codex の HTTP 取得に失敗しました") from exc
    if response.status_code in (401, 403):
        raise AuthRequired("Codex の認証が無効です")
    if response.status_code == 429:
        retry_after = None
        raw = response.headers.get("Retry-After")
        if raw:
            try:
                retry_after = float(raw)
            except ValueError:
                retry_after = None
        raise RateLimited(retry_after)
    if response.status_code >= 400:
        raise FetchError(f"Codex HTTP status={response.status_code}")
    try:
        payload = response.json()
    except ValueError as exc:
        raise FetchError("Codex の応答が JSON ではありません") from exc
    if not isinstance(payload, dict):
        raise FetchError("Codex の応答形式が想定と違います")
    return payload


def _codex_command() -> list[str] | None:
    for name in ("codex.cmd", "codex.exe", "codex"):
        path = shutil.which(name)
        if not path:
            continue
        if path.lower().endswith(".ps1"):
            return ["powershell.exe", "-NoProfile", "-NonInteractive", "-File", path]
        return [path]
    return None


def _fetch_app_server() -> dict[str, Any]:
    command = _codex_command()
    if not command:
        raise AuthRequired("codex CLI が見つかりません。codex login を実行してください")

    creationflags = CREATE_NO_WINDOW if sys.platform == "win32" else 0
    try:
        proc = subprocess.Popen(
            [*command, "app-server"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
            creationflags=creationflags,
        )
    except OSError as exc:
        raise FetchError("codex app-server を起動できませんでした") from exc

    lines: queue.Queue[str] = queue.Queue()

    def _reader() -> None:
        assert proc.stdout is not None
        try:
            for line in proc.stdout:
                lines.put(line)
        except Exception:
            pass

    thread = threading.Thread(target=_reader, daemon=True)
    thread.start()

    def send(message: dict[str, Any]) -> None:
        assert proc.stdin is not None
        proc.stdin.write(json.dumps(message, ensure_ascii=False) + "\n")
        proc.stdin.flush()

    def wait_id(expected: int, timeout: float) -> dict[str, Any]:
        deadline = time.time() + timeout
        while time.time() < deadline:
            remaining = deadline - time.time()
            try:
                line = lines.get(timeout=max(0.05, remaining))
            except queue.Empty:
                continue
            line = line.strip()
            if not line:
                continue
            try:
                data = json.loads(line)
            except json.JSONDecodeError:
                continue
            if not isinstance(data, dict):
                continue
            if data.get("id") == expected or data.get("id") == str(expected):
                return data
        raise FetchError("codex app-server の応答がありません")

    try:
        send(
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "clientInfo": {
                        "name": "ai-usage-bar",
                        "title": "AI Usage Bar",
                        "version": "0.1.0",
                    }
                },
            }
        )
        init = wait_id(1, 15.0)
        if init.get("error"):
            raise FetchError("codex app-server の初期化に失敗しました")
        send({"jsonrpc": "2.0", "method": "initialized", "params": {}})
        time.sleep(0.4)
        send({"jsonrpc": "2.0", "id": 2, "method": "account/rateLimits/read"})
        result = wait_id(2, 15.0)
        if result.get("error"):
            raise FetchError("Codex のレート制限を読めませんでした")
        payload = result.get("result")
        if not isinstance(payload, dict):
            raise FetchError("Codex app-server の応答形式が想定と違います")
        return payload
    finally:
        try:
            if proc.stdin:
                proc.stdin.close()
        except Exception:
            pass
        try:
            proc.terminate()
            proc.wait(timeout=3)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass


def _as_mapping(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


WINDOW_ALIASES = (
    ("primary", "5h"),
    ("secondary", "7d"),
    ("five_hour", "5h"),
    ("fiveHour", "5h"),
    ("five_hour_limit", "5h"),
    ("seven_day", "7d"),
    ("sevenDay", "7d"),
    ("weekly", "7d"),
    ("weekly_limit", "7d"),
)


def _extract_rate_limits(payload: dict[str, Any]) -> dict[str, Any]:
    if "rateLimits" in payload and isinstance(payload["rateLimits"], dict):
        return payload["rateLimits"]
    if "rate_limits" in payload and isinstance(payload["rate_limits"], dict):
        return payload["rate_limits"]
    nested = payload.get("rateLimitsByLimitId")
    if isinstance(nested, dict):
        for key in ("codex", "chatgpt"):
            item = nested.get(key)
            if isinstance(item, dict):
                return item
        for item in nested.values():
            if isinstance(item, dict):
                return item
    limits = payload.get("limits")
    if isinstance(limits, dict):
        return limits
    return payload


def _source_dicts(payload: dict[str, Any]) -> list[dict[str, Any]]:
    sources = [payload, _extract_rate_limits(payload)]
    extra = payload.get("limits")
    if isinstance(extra, dict):
        sources.append(extra)
    for source in list(sources):
        nested = source.get("codex")
        if isinstance(nested, dict):
            sources.append(nested)
    return sources


def _collect_windows(payload: dict[str, Any]) -> list[RateWindow]:
    windows: list[RateWindow] = []
    seen: set[str] = set()
    for source in _source_dicts(payload):
        for key, label in WINDOW_ALIASES:
            window = _window_from(source.get(key), label)
            if window and window.label not in seen:
                seen.add(window.label)
                windows.append(window)
        for list_key in ("windows", "rate_limit_windows"):
            items = source.get(list_key)
            if not isinstance(items, list):
                continue
            for item in items:
                window = _window_from(item, "win")
                if window and window.label not in seen:
                    seen.add(window.label)
                    windows.append(window)
    return windows


def _window_from(data: Any, default_label: str) -> RateWindow | None:
    mapping = _as_mapping(data)
    if not mapping:
        return None
    used = mapping.get("usedPercent")
    if used is None:
        used = mapping.get("used_percent")
    if used is None:
        used = mapping.get("utilization")
        if isinstance(used, (int, float)) and used <= 1.5:
            used = float(used) * 100.0
    used_percent = float(used) if isinstance(used, (int, float)) else None
    remaining = mapping.get("remainingPercent")
    if remaining is None:
        remaining = mapping.get("remaining_percent")
    if used_percent is None and isinstance(remaining, (int, float)):
        rem = float(remaining)
        if rem <= 1.5:
            rem *= 100.0
        used_percent = max(0.0, 100.0 - rem)
    duration = mapping.get("windowDurationMins")
    if duration is None:
        duration = mapping.get("window_duration_mins")
    duration_mins = int(duration) if isinstance(duration, (int, float)) else None
    label = default_label
    if duration_mins == 300:
        label = "5h"
    elif duration_mins == 10080:
        label = "7d"
    elif duration_mins == 1440:
        label = "1d"
    resets = parse_timestamp(mapping.get("resetsAt") or mapping.get("resets_at"))
    if used_percent is None and resets is None:
        return None
    return RateWindow(label=label, used_percent=used_percent, resets_at=resets, duration_mins=duration_mins)


def _credit_line(credits: dict[str, Any]) -> str | None:
    if not credits:
        return None
    if credits.get("unlimited"):
        return "Credits  無制限"
    if not credits.get("hasCredits"):
        return None
    balance = credits.get("balance")
    if balance in (None, "", "0", 0):
        return "Credits  あり"
    return f"Credits  {balance}"


def _snapshot_from_payload(payload: dict[str, Any], updated: datetime) -> ProviderSnapshot:
    limits = _extract_rate_limits(payload)
    windows = _collect_windows(payload)
    credits = _as_mapping(limits.get("credits") or payload.get("credits"))
    plan = limits.get("planType") or limits.get("plan_type") or payload.get("planType")
    plan_text = str(plan) if plan else None

    compact_parts = ["Codex"]
    if windows:
        compact_parts.append(" · ".join(item.compact() for item in windows))
    elif plan_text:
        compact_parts.append(plan_text)
    else:
        compact_parts.append("--")
    compact = " ".join(compact_parts)

    lines = ["Codex" + (f"  {plan_text}" if plan_text else "")]
    for window in windows:
        reset = format_reset(window.resets_at, updated)
        percent = format_percent(window.used_percent) if window.used_percent is not None else "--"
        when = ""
        if window.resets_at:
            local = window.resets_at.astimezone().strftime("%m/%d %H:%M")
            when = f"（{local}）"
        lines.append(f"{window.label}  {percent}  リセット {reset}{when}")
    credit_line = _credit_line(credits)
    if credit_line:
        lines.append(credit_line)
    lines.append(f"最終更新  {updated.astimezone().strftime('%H:%M:%S')}")

    used = windows[0].used_percent if windows else None
    return ProviderSnapshot(
        provider_id="codex",
        title="Codex",
        ok=True,
        compact=compact,
        lines=lines,
        updated_at=updated,
        used_percent=used,
    )
