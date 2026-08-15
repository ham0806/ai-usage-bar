from __future__ import annotations

from typing import Callable

from ai_usage_bar.config import AppConfig
from ai_usage_bar.models import ProviderSnapshot


class AuthRequired(Exception):
    pass


class RateLimited(Exception):
    def __init__(self, retry_after: float | None = None) -> None:
        super().__init__("rate limited")
        self.retry_after = retry_after


class FetchError(Exception):
    pass


ProviderFn = Callable[[AppConfig], ProviderSnapshot]
