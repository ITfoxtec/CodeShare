const { Issuer, custom, generators } = require('openid-client');

custom.setHttpOptionsDefaults({
  timeout: 10000
});

function createOidc(config) {
  let contextPromise;

  return {
    async getContext() {
      if (!contextPromise) {
        contextPromise = buildContext(config);
      }

      return contextPromise;
    }
  };
}

async function buildContext(config) {
  const issuer = await discoverIssuer(config.oidc.Authority);
  const client = new issuer.Client({
    client_id: config.oidc.ClientId,
    client_secret: config.oidc.ClientSecret,
    redirect_uris: [config.oidc.redirectUri],
    response_types: [config.oidc.ResponseType]
  });

  return {
    issuer,
    client
  };
}

async function discoverIssuer(authority) {
  try {
    return await Issuer.discover(authority);
  } catch (error) {
    console.warn(`OIDC discovery failed for ${authority}. Falling back to explicit FoxIDs endpoints.`);

    return new Issuer({
      issuer: authority,
      authorization_endpoint: `${authority}/oauth/authorize`,
      token_endpoint: `${authority}/oauth/token`,
      userinfo_endpoint: `${authority}/oauth/userinfo`
    });
  }
}

function createLoginState(session) {
  const state = generators.state();
  const nonce = generators.nonce();
  const codeVerifier = generators.codeVerifier();
  const codeChallenge = generators.codeChallenge(codeVerifier);

  session.oidc = {
    state,
    nonce,
    codeVerifier
  };

  return {
    state,
    nonce,
    codeChallenge
  };
}

function buildUserProfile(config, tokenSet, userInfoClaims) {
  const idTokenClaims = tokenSet.claims();
  const claims = {
    ...(userInfoClaims || {}),
    ...idTokenClaims
  };

  return {
    claims,
    displayName: claims[config.oidc.NameClaimType] || 'Authenticated user',
    roles: normalizeRoleClaims(claims[config.oidc.RoleClaimType])
  };
}

function normalizeRoleClaims(value) {
  if (Array.isArray(value)) {
    return value;
  }

  if (typeof value === 'string' && value.length > 0) {
    return [value];
  }

  return [];
}

function serializeTokenSet(tokenSet) {
  return {
    access_token: tokenSet.access_token,
    expires_at: tokenSet.expires_at,
    id_token: tokenSet.id_token,
    refresh_token: tokenSet.refresh_token,
    scope: tokenSet.scope,
    token_type: tokenSet.token_type
  };
}

module.exports = {
  buildUserProfile,
  createLoginState,
  createOidc,
  serializeTokenSet
};