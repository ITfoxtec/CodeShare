from __future__ import annotations

import os
from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent
SESSION_DIR = BASE_DIR / "var" / "flask_session"
SESSION_DIR.mkdir(parents=True, exist_ok=True)


def _env_flag(name: str, default: bool) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def _env_requests_verify(name: str, default: bool) -> bool | str:
    value = os.getenv(name)
    if value is None:
        return default

    normalized = value.strip()
    lowered = normalized.lower()
    if lowered in {"1", "true", "yes", "on"}:
        return True
    if lowered in {"0", "false", "no", "off"}:
        return False

    verify_path = Path(os.path.expandvars(normalized)).expanduser()
    if not verify_path.is_absolute():
        verify_path = BASE_DIR / verify_path
    return str(verify_path)


class Config:
    SECRET_KEY = os.getenv("FLASK_SECRET_KEY", "dev-change-me")

    SESSION_TYPE = "filesystem"
    SESSION_FILE_DIR = str(SESSION_DIR)
    SESSION_PERMANENT = False
    SESSION_USE_SIGNER = True
    SESSION_COOKIE_HTTPONLY = True
    SESSION_COOKIE_SAMESITE = "Lax"
    SESSION_COOKIE_SECURE = _env_flag("SESSION_COOKIE_SECURE", False)

    APP_BASE_URL = os.getenv("APP_BASE_URL", "http://localhost:5000").rstrip("/")

    FOXIDS_AUTHORITY = os.getenv("FOXIDS_AUTHORITY", "").rstrip("/")
    FOXIDS_CLIENT_ID = os.getenv("FOXIDS_CLIENT_ID", "")
    FOXIDS_CLIENT_SECRET = os.getenv("FOXIDS_CLIENT_SECRET", "")
    FOXIDS_USE_DISCOVERY = _env_flag("FOXIDS_USE_DISCOVERY", True)
    FOXIDS_VERIFY_TLS = _env_requests_verify("FOXIDS_VERIFY_TLS", True)

