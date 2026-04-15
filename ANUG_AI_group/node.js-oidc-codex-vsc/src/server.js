const express = require('express');
const session = require('express-session');
const MemoryStoreFactory = require('memorystore');

const { config } = require('./config');
const { buildUserProfile, createLoginState, createOidc, serializeTokenSet } = require('./oidc');

const MemoryStore = MemoryStoreFactory(session);
const oidc = createOidc(config);
const app = express();

if (config.isProduction && config.session.store === 'memory') {
  throw new Error('The in-memory session store is for local development only. Configure a durable server-side session store before deploying to production or serverless hosting.');
}

app.disable('x-powered-by');
app.set('trust proxy', 1);

app.use(express.json());
app.use(express.urlencoded({ extended: false }));
app.use(
  session({
    cookie: {
      httpOnly: true,
      maxAge: config.session.ttlMilliseconds,
      sameSite: 'lax',
      secure: config.isProduction
    },
    name: config.session.name,
    resave: false,
    rolling: true,
    saveUninitialized: false,
    secret: config.session.secret,
    store: new MemoryStore({
      checkPeriod: config.session.ttlMilliseconds
    })
  })
);

app.get('/', (req, res) => {
  res.type('html').send(renderHomePage(req));
});

app.get('/auth/login', async (req, res, next) => {
  try {
    const { client } = await oidc.getContext();
    const { codeChallenge, nonce, state } = createLoginState(req.session);
    const returnTo = getSafeReturnTo(req.query.returnTo);

    if (returnTo) {
      req.session.returnTo = returnTo;
    }

    const authorizationUrl = client.authorizationUrl({
      code_challenge: codeChallenge,
      code_challenge_method: 'S256',
      nonce,
      redirect_uri: config.oidc.redirectUri,
      response_type: config.oidc.ResponseType,
      scope: config.oidc.Scopes,
      state
    });

    res.redirect(authorizationUrl);
  } catch (error) {
    next(error);
  }
});

app.get('/auth/callback', async (req, res, next) => {
  try {
    const loginState = req.session.oidc;

    if (!loginState) {
      res.status(400).type('html').send(renderErrorPage('The sign-in flow expired. Start again from the home page.'));
      return;
    }

    const { client } = await oidc.getContext();
    const params = client.callbackParams(req);
    const tokenSet = await client.callback(config.oidc.redirectUri, params, {
      code_verifier: loginState.codeVerifier,
      nonce: loginState.nonce,
      state: loginState.state
    });
    const returnTo = req.session.returnTo || '/';
    let userInfoClaims = {};

    if (tokenSet.access_token) {
      try {
        userInfoClaims = await client.userinfo(tokenSet.access_token);
      } catch (error) {
        console.warn('The user info endpoint did not return claims. Continuing with JWT claims only.');
      }
    }

    const user = buildUserProfile(config, tokenSet, userInfoClaims);

    await regenerateSession(req);

    req.session.auth = {
      isAuthenticated: true,
      tokens: serializeTokenSet(tokenSet),
      user
    };
    res.redirect(returnTo);
  } catch (error) {
    next(error);
  }
});

app.get('/auth/logout', async (req, res, next) => {
  try {
    const { client } = await oidc.getContext();
    const idTokenHint = req.session.auth && req.session.auth.tokens ? req.session.auth.tokens.id_token : undefined;
    const hasProviderLogout = Boolean(client.issuer.metadata.end_session_endpoint);
    const logoutUrl = hasProviderLogout
      ? client.endSessionUrl({
          id_token_hint: idTokenHint,
          post_logout_redirect_uri: config.oidc.postLogoutRedirectUri
        })
      : config.oidc.postLogoutRedirectUri;

    await destroySession(req);
    res.clearCookie(config.session.name);
    res.redirect(logoutUrl);
  } catch (error) {
    next(error);
  }
});

app.get('/api/protected', requireAuthenticatedApi, (req, res) => {
  const claims = req.session.auth.user.claims;

  res.json({
    email: claims.email || null,
    message: 'This protected data is only returned for an authenticated session cookie.',
    role: claims[config.oidc.RoleClaimType] || null,
    sub: claims[config.oidc.NameClaimType] || null,
    timestamp: new Date().toISOString()
  });
});

app.use((error, req, res, next) => {
  console.error(error);

  if (res.headersSent) {
    next(error);
    return;
  }

  res.status(500).type('html').send(renderErrorPage(error.message || 'An unexpected error occurred.'));
});

app.listen(config.port, () => {
  console.log(`Server listening on ${config.baseUrl}`);
  console.log(`FoxIDs redirect URI: ${config.oidc.redirectUri}`);
});

function requireAuthenticatedApi(req, res, next) {
  if (!req.session.auth || !req.session.auth.isAuthenticated) {
    res.status(401).json({
      error: 'Authentication required.'
    });
    return;
  }

  next();
}

function getSafeReturnTo(value) {
  if (typeof value !== 'string') {
    return null;
  }

  if (!value.startsWith('/') || value.startsWith('//')) {
    return null;
  }

  return value;
}

function regenerateSession(req) {
  return new Promise((resolve, reject) => {
    req.session.regenerate((error) => {
      if (error) {
        reject(error);
        return;
      }

      resolve();
    });
  });
}

function destroySession(req) {
  return new Promise((resolve, reject) => {
    req.session.destroy((error) => {
      if (error) {
        reject(error);
        return;
      }

      resolve();
    });
  });
}

function renderHomePage(req) {
  const isAuthenticated = Boolean(req.session.auth && req.session.auth.isAuthenticated);
  const claims = isAuthenticated ? req.session.auth.user.claims : null;
  const roles = isAuthenticated ? req.session.auth.user.roles : [];

  return `<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>FoxIDs OIDC Sample</title>
    <style>
      :root {
        color-scheme: light;
        font-family: "Segoe UI", sans-serif;
        --accent: #0d5f52;
        --accent-soft: #d6efe8;
        --ink: #172026;
        --paper: #f4f1e8;
        --panel: #ffffff;
        --warn: #8a4b16;
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
        min-height: 100vh;
        background: radial-gradient(circle at top, #d8ead9 0%, var(--paper) 45%, #ece4d3 100%);
        color: var(--ink);
      }

      main {
        max-width: 960px;
        margin: 0 auto;
        padding: 32px 20px 56px;
      }

      .hero,
      .panel {
        background: rgba(255, 255, 255, 0.92);
        border: 1px solid rgba(13, 95, 82, 0.12);
        border-radius: 20px;
        box-shadow: 0 18px 60px rgba(23, 32, 38, 0.08);
      }

      .hero {
        padding: 28px;
      }

      .eyebrow {
        display: inline-block;
        margin-bottom: 12px;
        padding: 6px 10px;
        border-radius: 999px;
        background: var(--accent-soft);
        color: var(--accent);
        font-size: 12px;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }

      h1,
      h2 {
        margin: 0 0 12px;
      }

      p {
        line-height: 1.6;
      }

      .actions {
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
        margin-top: 20px;
      }

      .button {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 12px 18px;
        border-radius: 999px;
        border: 0;
        background: var(--accent);
        color: #ffffff;
        font: inherit;
        font-weight: 700;
        text-decoration: none;
      }

      .button.secondary {
        background: #24343d;
      }

      .grid {
        display: grid;
        gap: 20px;
        margin-top: 24px;
      }

      .panel {
        padding: 24px;
      }

      .warning {
        margin: 0 0 16px;
        padding: 12px 14px;
        border-radius: 12px;
        background: #fff3e4;
        color: var(--warn);
        font-weight: 600;
      }

      pre {
        margin: 0;
        padding: 16px;
        overflow: auto;
        border-radius: 14px;
        background: #142129;
        color: #d8f2ec;
      }

      .meta {
        display: flex;
        gap: 16px;
        flex-wrap: wrap;
        margin-bottom: 16px;
        color: #4f5c64;
      }

      @media (max-width: 720px) {
        main {
          padding: 20px 14px 40px;
        }

        .hero,
        .panel {
          border-radius: 16px;
        }
      }
    </style>
  </head>
  <body>
    <main>
      <section class="hero">
        <span class="eyebrow">FoxIDs OpenID Connect</span>
        <h1>Minimal Node.js web app with server-side sign-in</h1>
        <p>
          This sample uses the authorization code flow with response type <strong>code</strong>, discovery-first metadata,
          JWT claims from the ID token, and a session cookie that keeps tokens on the server.
        </p>
        <div class="actions">
          ${isAuthenticated ? '<a class="button secondary" href="/auth/logout">Log off</a>' : '<a class="button" href="/auth/login">Log in</a>'}
        </div>
      </section>

      <section class="grid">
        <article class="panel">
          <h2>Authentication state</h2>
          ${isAuthenticated ? `<p>You are signed in as <strong>${escapeHtml(req.session.auth.user.displayName)}</strong>.</p>` : '<p>You are anonymous. Protected data stays hidden until you sign in.</p>'}
          ${isAuthenticated ? `<div class="meta"><span>sub: ${escapeHtml(String(claims[config.oidc.NameClaimType] || 'n/a'))}</span><span>role: ${escapeHtml(JSON.stringify(roles))}</span></div>` : ''}
        </article>

        ${isAuthenticated ? `<article class="panel"><h2>Protected API data</h2><pre id="protected-data">Loading protected data...</pre></article>` : ''}

        ${isAuthenticated ? `<article class="panel"><h2>Debug claims</h2><p class="warning">Temporary debug display. Remove this claims output before shipping to production.</p><pre>${escapeHtml(JSON.stringify(claims, null, 2))}</pre></article>` : ''}
      </section>
    </main>
    ${isAuthenticated ? renderProtectedFetchScript() : ''}
  </body>
</html>`;
}

function renderProtectedFetchScript() {
  return `<script>
    fetch('/api/protected', {
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json'
      }
    })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error('Protected fetch failed with status ' + response.status);
        }

        return response.json();
      })
      .then((data) => {
        document.getElementById('protected-data').textContent = JSON.stringify(data, null, 2);
      })
      .catch((error) => {
        document.getElementById('protected-data').textContent = error.message;
      });
  </script>`;
}

function renderErrorPage(message) {
  return `<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>FoxIDs OIDC Error</title>
    <style>
      body {
        margin: 0;
        min-height: 100vh;
        display: grid;
        place-items: center;
        background: #f3eee3;
        color: #1d2428;
        font-family: "Segoe UI", sans-serif;
      }

      main {
        max-width: 640px;
        padding: 24px;
      }

      article {
        background: #ffffff;
        border-radius: 18px;
        padding: 24px;
        box-shadow: 0 16px 50px rgba(29, 36, 40, 0.08);
      }

      a {
        color: #0d5f52;
        font-weight: 700;
      }
    </style>
  </head>
  <body>
    <main>
      <article>
        <h1>Authentication error</h1>
        <p>${escapeHtml(message)}</p>
        <p><a href="/">Return to the home page</a></p>
      </article>
    </main>
  </body>
</html>`;
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}