const dotenv = require('dotenv');

dotenv.config();

function readRequired(name) {
  const value = process.env[name];

  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value;
}

function readNumber(name, fallback) {
  const raw = process.env[name];

  if (!raw) {
    return fallback;
  }

  const value = Number(raw);

  if (!Number.isFinite(value)) {
    throw new Error(`Environment variable ${name} must be a number.`);
  }

  return value;
}

const baseUrl = (process.env.BASE_URL || 'http://localhost:3000').replace(/\/+$/, '');
const isProduction = process.env.NODE_ENV === 'production';

const config = {
  port: readNumber('PORT', 3000),
  baseUrl,
  isProduction,
  session: {
    name: process.env.SESSION_COOKIE_NAME || 'foxids.sid',
    secret: readRequired('SESSION_SECRET'),
    store: process.env.SESSION_STORE || 'memory',
    ttlMilliseconds: readNumber('SESSION_TTL_MS', 8 * 60 * 60 * 1000)
  },
  oidc: {
    Authority: readRequired('AUTHORITY').replace(/\/+$/, ''),
    ClientId: readRequired('CLIENT_ID'),
    ClientSecret: readRequired('CLIENT_SECRET'),
    ResponseType: 'code',
    Scopes: 'openid profile email',
    NameClaimType: 'sub',
    RoleClaimType: 'role',
    redirectUri: `${baseUrl}/auth/callback`,
    postLogoutRedirectUri: `${baseUrl}/`
  }
};

module.exports = {
  config
};