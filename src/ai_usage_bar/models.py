from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any


@dataclass(frozen=True)
class RateWindow:
    label: str
    used_percent: float | None
    resets_at: datetime | None = None
    duration_mins: int | None = None

    def compact(self) -> str:
        if self.used_percent is None:
            return f"{self.label} --"
        return f"{self.label} {format_percent(self.used_percent)}"


@dataclass
class ProviderSnapshot:
    provider_id: str
    title: str
    ok: bool
    compact: str
    lines: list[str] = field(default_factory=list)
    error: str | None = None
    updated_at: datetime | None = None
    used_percent: float | None = None


def format_percent(value: float) -> str:
    if value < 0:
        value = 0
    if value > 999:
        value = 999
    if abs(value - round(value)) < 0.05:
        return f"{round(value):.0f}%"
    return f"{value:.1f}%"


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def parse_timestamp(value: Any) -> datetime | None:
    if value is None or value == "":
        return None
    if isinstance(value, datetime):
        if value.tzinfo is None:
            return value.replace(tzinfo=timezone.utc)
        return value.astimezone(timezone.utc)
    if isinstance(value, (int, float)):
        ts = float(value)
        if ts > 1e12:
            ts /= 1000.0
        try:
            return datetime.fromtimestamp(ts, tz=timezone.utc)
        except (OSError, OverflowError, ValueError):
            return None
    if isinstance(value, str):
        text = value.strip()
        if not text:
            return None
        if text.isdigit():
            return parse_timestamp(int(text))
        try:
            return datetime.fromisoformat(text.replace("Z", "+00:00")).astimezone(timezone.utc)
        except ValueError:
            return None
    return None


def format_reset(resets_at: datetime | None, now: datetime | None = None) -> str:
    if resets_at is None:
        return "不明"
    now = now or utcnow()
    delta = resets_at - now
    seconds = int(delta.total_seconds())
    if seconds <= 0:
        return "まもなく"
    minutes, _ = divmod(seconds, 60)
    hours, minutes = divmod(minutes, 60)
    days, hours = divmod(hours, 24)
    if days:
        return f"{days}日{hours}時間後"
    if hours:
        return f"{hours}時間{minutes}分後"
    return f"{minutes}分後"


def jwt_payload(token: str) -> dict[str, Any]:
    import base64

    parts = token.split(".")
    if len(parts) < 2:
        return {}
    payload = parts[1]
    pad = "=" * (-len(payload) % 4)
    try:
        raw = base64.urlsafe_b64decode(payload + pad)
        data = json.loads(raw)
    except (ValueError, json.JSONDecodeError):
        return {}
    return data if isinstance(data, dict) else {}


def compose_compact(snapshots: list[ProviderSnapshot]) -> str:
    parts = [item.compact for item in snapshots if item.compact]
    return "  |  ".join(parts) if parts else "AI Usage Bar"
