from __future__ import annotations

import logging
from datetime import datetime
from typing import Any

import httpx

from ai_usage_bar.auth import as_safe_error, get_cursor_token
from ai_usage_bar.auth.cursor_token import load_cursor_cookie, normalize_session_cookie
from ai_usage_bar.models import ProviderSnapshot, format_percent, parse_timestamp, utcnow
from ai_usage_bar.providers import AuthRequired, FetchError, RateLimited

log = logging.getLogger(__name__)

USAGE_URL = "https://cursor.com/api/usage-summary"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)


def fetch_cursor() -> ProviderSnapshot:
    updated = utcnow()
    try:
        cookie = load_cursor_cookie()
        if not cookie:
            raise AuthRequired("Cursor のセッショントークンが見つかりません")
        try:
            payload = _fetch_summary(cookie)
        except AuthRequired:
            saved = get_cursor_token()
            fallback = normalize_session_cookie(saved) if saved else None
            if not fallback or fallback == cookie:
                raise
            payload = _fetch_summary(fallback)
        return _snapshot_from_payload(payload, updated)
    except AuthRequired as exc:
        return ProviderSnapshot(
            provider_id="cursor",
            title="Cursor",
            ok=False,
            compact="Cursor 要認証",
            lines=[
                "Cursor",
                as_safe_error(exc) or "セッショントークンが必要です",
                "cursor.com にログインするか、設定から Cookie を貼り付けてください",
            ],
            error="要認証",
            updated_at=updated,
        )
    except RateLimited:
        return ProviderSnapshot(
            provider_id="cursor",
            title="Cursor",
            ok=False,
            compact="Cursor 制限中",
            lines=["Cursor", "レート制限中です。しばらく待ってから再取得します"],
            error="レート制限",
            updated_at=updated,
        )
    except FetchError as exc:
        return ProviderSnapshot(
            provider_id="cursor",
            title="Cursor",
            ok=False,
            compact="Cursor --",
            lines=["Cursor", as_safe_error(exc)],
            error=as_safe_error(exc),
            updated_at=updated,
        )
    except Exception:
        log.exception("Cursor の使用量取得で予期しないエラー")
        return ProviderSnapshot(
            provider_id="cursor",
            title="Cursor",
            ok=False,
            compact="Cursor --",
            lines=["Cursor", "取得に失敗しました"],
            error="取得失敗",
            updated_at=updated,
        )


def _fetch_summary(cookie: str) -> dict[str, Any]:
    headers = {
        "Cookie": f"WorkosCursorSessionToken={cookie}",
        "Accept": "application/json",
        "User-Agent": USER_AGENT,
        "Origin": "https://cursor.com",
        "Referer": "https://cursor.com/dashboard/usage",
    }
    try:
        response = httpx.get(USAGE_URL, headers=headers, timeout=20.0)
    except httpx.HTTPError as exc:
        raise FetchError("Cursor の HTTP 取得に失敗しました") from exc
    if response.status_code in (401, 403):
        raise AuthRequired("Cursor のセッショントークンが無効です")
    if response.status_code == 429:
        raise RateLimited()
    if response.status_code >= 400:
        raise FetchError(f"Cursor HTTP status={response.status_code}")
    try:
        payload = response.json()
    except ValueError as exc:
        raise FetchError("Cursor の応答が JSON ではありません") from exc
    if isinstance(payload, dict) and payload.get("error") == "not_authenticated":
        raise AuthRequired("Cursor のセッショントークンが無効です")
    if not isinstance(payload, dict):
        raise FetchError("Cursor の応答形式が想定と違います")
    return payload


def _number(value: Any) -> float | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    return None


def _plan_percent(plan: dict[str, Any]) -> float | None:
    for key in ("totalPercentUsed", "apiPercentUsed", "autoPercentUsed"):
        value = _number(plan.get(key))
        if value is not None:
            return value
    used = _number(plan.get("used"))
    limit = _number(plan.get("limit"))
    if used is not None and limit:
        return used / limit * 100.0
    remaining = _number(plan.get("remaining"))
    if used is not None and remaining is not None and (used + remaining) > 0:
        return used / (used + remaining) * 100.0
    return None


def _format_cycle(start: datetime | None, end: datetime | None) -> str | None:
    if not start and not end:
        return None
    def fmt(value: datetime | None) -> str:
        if value is None:
            return "?"
        return value.astimezone().strftime("%Y-%m-%d")
    return f"{fmt(start)} ～ {fmt(end)}"


def _snapshot_from_payload(payload: dict[str, Any], updated: datetime) -> ProviderSnapshot:
    membership = payload.get("membershipType") or payload.get("plan") or payload.get("membership")
    unlimited = bool(payload.get("isUnlimited"))
    individual = payload.get("individualUsage") if isinstance(payload.get("individualUsage"), dict) else {}
    plan = individual.get("plan") if isinstance(individual.get("plan"), dict) else {}
    on_demand = individual.get("onDemand") if isinstance(individual.get("onDemand"), dict) else {}
    if not plan and isinstance(payload.get("plan"), dict):
        plan = payload["plan"]

    percent = _plan_percent(plan) if plan else _number(payload.get("totalPercentUsed"))
    if unlimited:
        compact = "Cursor ∞"
        used_percent = 0.0
    elif percent is None:
        compact = "Cursor --"
        used_percent = None
    else:
        compact = f"Cursor {format_percent(percent)}"
        used_percent = percent

    lines = ["Cursor" + (f"  {membership}" if membership else "")]
    if unlimited:
        lines.append("プラン  無制限")
    elif percent is not None:
        used = _number(plan.get("used"))
        limit = _number(plan.get("limit"))
        extra = ""
        if used is not None and limit is not None and limit > 0:
            extra = f"（{used:.0f}/{limit:.0f}）"
        lines.append(f"プラン  {format_percent(percent)}{extra}")
    cycle = _format_cycle(
        parse_timestamp(payload.get("billingCycleStart")),
        parse_timestamp(payload.get("billingCycleEnd")),
    )
    if cycle:
        lines.append(f"課金周期  {cycle}")
    if on_demand.get("enabled"):
        used = _number(on_demand.get("used"))
        limit = _number(on_demand.get("limit"))
        if used is not None and limit:
            lines.append(f"オンデマンド  {used:.0f}/{limit:.0f}")
        elif used is not None:
            lines.append(f"オンデマンド  {used:.0f}")
    message = payload.get("namedModelSelectedDisplayMessage") or payload.get("autoModelSelectedDisplayMessage")
    if percent is None and isinstance(message, str) and message.strip():
        lines.append(message.strip())
    lines.append(f"最終更新  {updated.astimezone().strftime('%H:%M:%S')}")

    return ProviderSnapshot(
        provider_id="cursor",
        title="Cursor",
        ok=percent is not None or unlimited,
        compact=compact,
        lines=lines,
        updated_at=updated,
        used_percent=used_percent,
        error=None if (percent is not None or unlimited) else "使用量を解釈できませんでした",
    )
