from __future__ import annotations

import json
from functools import wraps
from typing import Any
from urllib.parse import urljoin

import requests
from authlib.integrations.flask_client import OAuth
from dotenv import load_dotenv
from flask import Flask, flash, redirect, render_template, session, url_for
from flask_session import Session

load_dotenv(override=True)

from config import Config


AUTH_SCOPE = "openid profile email"


def _missing_settings(app: Flask) -> list[str]:
    missing: list[str] = []
    if not app.config["FOXIDS_AUTHORITY"]:
        missing.append("FOXIDS_AUTHORITY")
    if not app.config["FOXIDS_CLIENT_ID"]:
        missing.append("FOXIDS_CLIENT_ID")
    if not app.config["FOXIDS_CLIENT_SECRET"]:
        missing.append("FOXIDS_CLIENT_SECRET")
    return missing


def _oidc_ready(app: Flask) -> bool:
    return not _missing_settings(app)


def _normalize_roles(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(item) for item in value]
    if isinstance(value, tuple):
        return [str(item) for item in value]
    if isinstance(value, str):
        return [item.strip() for item in value.split(",") if item.strip()]
    return [str(value)]


def _build_user(claims: dict[str, Any]) -> dict[str, Any]:
    return {
        "sub": claims.get("sub", ""),
        "email": claims.get("email", ""),
        "roles": _normalize_roles(claims.get("role")),
        "claims": claims,
    }


def _login_required(view_func):
    @wraps(view_func)
    def wrapped(*args, **kwargs):
        if "user" not in session:
            flash("Log in to view protected data.", "warning")
            return redirect(url_for("index"))
        return view_func(*args, **kwargs)

    return wrapped


def _fetch_fallback_userinfo(
    authority: str, access_token: str, verify_tls: bool | str
) -> dict[str, Any]:
    response = requests.get(
        f"{authority}/oauth/userinfo",
        headers={"Authorization": f"Bearer {access_token}"},
        timeout=10,
        verify=verify_tls,
    )
    response.raise_for_status()
    return response.json()


def _absolute_url(app: Flask, path: str) -> str:
    base_url = app.config["APP_BASE_URL"].rstrip("/") + "/"
    return urljoin(base_url, path.lstrip("/"))


def _register_foxids(oauth: OAuth, app: Flask):
    if not _oidc_ready(app):
        return None

    register_kwargs: dict[str, Any] = {
        "name": "foxids",
        "client_id": app.config["FOXIDS_CLIENT_ID"],
        "client_secret": app.config["FOXIDS_CLIENT_SECRET"],
        "client_kwargs": {
            "scope": AUTH_SCOPE,
            "verify": app.config["FOXIDS_VERIFY_TLS"],
        },
        "authorize_params": {"response_type": "code"},
    }

    authority = app.config["FOXIDS_AUTHORITY"]
    if app.config["FOXIDS_USE_DISCOVERY"]:
        register_kwargs["server_metadata_url"] = (
            f"{authority}/.well-known/openid-configuration"
        )
    else:
        register_kwargs["authorize_url"] = f"{authority}/oauth/authorize"
        register_kwargs["access_token_url"] = f"{authority}/oauth/token"

    return oauth.register(**register_kwargs)


def create_app() -> Flask:
    app = Flask(__name__)
    app.config.from_object(Config)

    Session(app)
    oauth = OAuth(app)
    foxids = _register_foxids(oauth, app)

    @app.context_processor
    def inject_template_state() -> dict[str, Any]:
        return {
            "current_user": session.get("user"),
            "is_configured": _oidc_ready(app),
        }

    @app.get("/")
    def index():
        redirect_uri = _absolute_url(app, "/auth/callback")
        protected_data = None

        if "user" in session:
            user = session["user"]
            protected_data = {
                "subject": user.get("sub"),
                "email": user.get("email"),
                "roles": user.get("roles", []),
            }

        return render_template(
            "index.html",
            configured=_oidc_ready(app),
            missing_settings=_missing_settings(app),
            redirect_uri=redirect_uri,
            protected_data=protected_data,
            authority=app.config["FOXIDS_AUTHORITY"],
            use_discovery=app.config["FOXIDS_USE_DISCOVERY"],
        )

    @app.get("/login")
    def login():
        if not foxids:
            flash(
                "Set FOXIDS_AUTHORITY, FOXIDS_CLIENT_ID, and FOXIDS_CLIENT_SECRET first.",
                "error",
            )
            return redirect(url_for("index"))

        try:
            return foxids.authorize_redirect(
                _absolute_url(app, "/auth/callback"),
                response_type="code",
            )
        except Exception as exc:
            flash(f"FoxIDs login could not start: {exc}", "error")
            return redirect(url_for("index"))

    @app.get("/auth/callback")
    def auth_callback():
        if not foxids:
            return redirect(url_for("index"))

        try:
            token = foxids.authorize_access_token()
            claims = token.get("userinfo")

            if not claims and token.get("id_token") and app.config["FOXIDS_USE_DISCOVERY"]:
                claims = foxids.parse_id_token(token)

            if not claims:
                claims = _fetch_fallback_userinfo(
                    app.config["FOXIDS_AUTHORITY"],
                    token["access_token"],
                    app.config["FOXIDS_VERIFY_TLS"],
                )

            session["token"] = {
                key: token.get(key)
                for key in (
                    "access_token",
                    "expires_at",
                    "id_token",
                    "refresh_token",
                    "scope",
                    "token_type",
                )
            }
            session["user"] = _build_user(claims)
            flash("Logged in with FoxIDs.", "success")
        except Exception as exc:
            flash(f"FoxIDs callback failed: {exc}", "error")

        return redirect(url_for("index"))

    @app.get("/logout")
    def logout():
        id_token = (session.get("token") or {}).get("id_token")
        session.pop("user", None)
        session.pop("token", None)

        if foxids and id_token and app.config["FOXIDS_USE_DISCOVERY"]:
            try:
                return foxids.logout_redirect(
                    post_logout_redirect_uri=_absolute_url(app, "/logged-out"),
                    id_token_hint=id_token,
                )
            except Exception:
                pass

        flash("Logged out.", "success")
        return redirect(url_for("index"))

    @app.get("/logged-out")
    def logged_out():
        if foxids and app.config["FOXIDS_USE_DISCOVERY"]:
            try:
                foxids.validate_logout_response()
            except Exception:
                pass

        flash("Logged out.", "success")
        return redirect(url_for("index"))

    @app.get("/api/protected")
    @_login_required
    def protected_api():
        user = session["user"]
        return {
            "message": "This protected payload is only available to authenticated users.",
            "subject": user.get("sub"),
            "email": user.get("email"),
            "roles": user.get("roles", []),
            "name_claim_type": "sub",
            "role_claim_type": "role",
        }

    @app.get("/debug/session")
    @_login_required
    def debug_session():
        return app.response_class(
            json.dumps(session["user"]["claims"], indent=2),
            mimetype="application/json",
        )

    return app


app = create_app()


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=True)
