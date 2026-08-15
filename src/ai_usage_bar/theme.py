from __future__ import annotations

from dataclasses import dataclass

import darkdetect
import winreg


@dataclass(frozen=True)
class Theme:
    bg: str
    fg: str
    muted: str
    accent: str
    warn: str
    bad: str
    ok: str
    border: str
    flyout_bg: str


DARK = Theme(
    bg="#202020",
    fg="#f3f3f3",
    muted="#c8c8c8",
    accent="#60cdff",
    warn="#fce100",
    bad="#ff99a4",
    ok="#6ccb5f",
    border="#3d3d3d",
    flyout_bg="#2c2c2c",
)

LIGHT = Theme(
    bg="#f3f3f3",
    fg="#1a1a1a",
    muted="#5d5d5d",
    accent="#0037da",
    warn="#9d5d00",
    bad="#c42b1c",
    ok="#0f7b0f",
    border="#d1d1d1",
    flyout_bg="#ffffff",
)


def _system_uses_light_theme() -> bool:
    try:
        with winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        ) as key:
            value, _ = winreg.QueryValueEx(key, "SystemUsesLightTheme")
            return bool(value)
    except OSError:
        return not bool(darkdetect.isDark())


def current_theme() -> Theme:
    return LIGHT if _system_uses_light_theme() else DARK


def percent_color(theme: Theme, used_percent: float | None) -> str:
    if used_percent is None:
        return theme.muted
    if used_percent >= 90:
        return theme.bad
    if used_percent >= 70:
        return theme.warn
    return theme.ok
