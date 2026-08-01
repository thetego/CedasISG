const allowedProviders = new Set(['demo', 'playfab']);

function integer(value, fallback) {
  const parsed = Number.parseInt(value || '', 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

export function validGatewayUrl(value) {
  try {
    const url = new URL(value);
    if (url.username || url.password || url.hash) return false;
    if (url.protocol === "https:") return true;
    return url.protocol === "http:" && ["127.0.0.1", "localhost", "[::1]"].includes(url.hostname);
  } catch {
    return false;
  }
}

export function loadConfig(env = process.env) {
  const provider = (env.DATA_PROVIDER || 'demo').toLowerCase();
  if (!allowedProviders.has(provider)) throw new Error(`Unsupported DATA_PROVIDER: ${provider}`);

  const config = {
    provider,
    port: integer(env.PORT, 4173),
    host: env.HOST || '127.0.0.1',
    origin: env.APP_ORIGIN || `http://${env.HOST || '127.0.0.1'}:${integer(env.PORT, 4173)}`,
    sessionSecret: env.SESSION_SECRET || 'demo-only-session-secret-change-in-production',
    settingsFile: env.RUNTIME_SETTINGS_FILE || '.data/runtime-settings.enc',
    auditFile: env.AUDIT_LOG_FILE || '.data/audit.ndjson',
    privacyFile: env.PRIVACY_REQUESTS_FILE || '.data/privacy-requests.enc',
    demoPassword: env.DEMO_PASSWORD || 'demo123',
    playfab: {
      titleId: env.PLAYFAB_TITLE_ID || '',
      dataUrl: env.PLAYFAB_DATA_URL || '',
      serviceToken: env.PLAYFAB_SERVICE_TOKEN || '',
      timeoutMs: integer(env.PLAYFAB_TIMEOUT_MS, 15000)
    }
  };

  if (env.NODE_ENV === 'production' && config.sessionSecret === 'demo-only-session-secret-change-in-production') {
    throw new Error('SESSION_SECRET is required in production');
  }
  if (provider === 'playfab') {
    const missing = Object.entries(config.playfab)
      .filter(([key, value]) => key !== 'timeoutMs' && !value)
      .map(([key]) => key);
    if (missing.length) throw new Error(`PlayFab configuration missing: ${missing.join(', ')}`);
    if (config.sessionSecret.length < 32) throw new Error('SESSION_SECRET must contain at least 32 characters');
    if (!/^[A-Za-z0-9]+$/.test(config.playfab.titleId)) throw new Error('PLAYFAB_TITLE_ID is invalid');
    if (config.playfab.serviceToken.length < 32) throw new Error('PLAYFAB_SERVICE_TOKEN must contain at least 32 characters');
    if (!validGatewayUrl(config.playfab.dataUrl)) throw new Error('PLAYFAB_DATA_URL must use HTTPS or loopback HTTP');
  }
  return Object.freeze(config);
}
