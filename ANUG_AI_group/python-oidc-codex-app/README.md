# FoxIDs OpenID Connect Flask sample

This workspace contains a minimal server-rendered Flask application configured for FoxIDs OpenID Connect.

## What it does

- Uses server-side sessions backed by the filesystem under `var/flask_session`
- Uses authorization code flow with response type `code`
- Requests `openid profile email`
- Stores tokens on the server, not in the browser
- Shows `sub`, `email`, `role`, and the raw claims after login for temporary debugging
- Hides protected data and the protected API payload from anonymous users

## Local setup on Windows

1. Create and activate a virtual environment:

   ```powershell
   py -m venv .venv
   .\.venv\Scripts\Activate.ps1
   ```

2. Install dependencies:

   ```powershell
   pip install -r requirements.txt
   ```

3. Copy `.env.example` to `.env` and fill in the FoxIDs values.
   Set `APP_BASE_URL` to the exact public origin you will use for the app.

4. Run the app:

   ```powershell
   python app.py
   ```

5. Open `http://localhost:5000`.

## Required FoxIDs values

Set these in `.env`:

- `FOXIDS_AUTHORITY`
- `FOXIDS_CLIENT_ID`
- `FOXIDS_CLIENT_SECRET`

The redirect URI is derived from `APP_BASE_URL`.

The default local redirect URI is:

```text
http://localhost:5000/auth/callback
```

If you disable discovery with `FOXIDS_USE_DISCOVERY=false`, the app falls back to:

- Authorize endpoint: `Authority + /oauth/authorize`
- Token endpoint: `Authority + /oauth/token`
- User info endpoint: `Authority + /oauth/userinfo`
