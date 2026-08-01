import { validateBootstrap } from '../lib/event-schema.js';

export class PlayFabProvider {
  constructor(config) {
    this.config = config;
    this.name = 'playfab';
    this.cache = null;
    this.cacheUntil = 0;
  }

  async request(path = '', { method = 'GET', body } = {}) {
    let lastError;
    for (let attempt = 0; attempt < 3; attempt++) {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), this.config.timeoutMs);
      try {
        const url = new URL(path, this.config.dataUrl);
        url.searchParams.set('titleId', this.config.titleId);
        const response = await fetch(url, {
          method,
          headers: {
            Authorization: `Bearer ${this.config.serviceToken}`,
            Accept: 'application/json',
            ...(body ? { 'Content-Type': 'application/json' } : {}),
          },
          body: body ? JSON.stringify(body) : undefined,
          signal: controller.signal,
        });
        if (response.ok) return await response.json();
        if (response.status < 500 && response.status !== 429)
          throw Object.assign(new Error(`PlayFab gateway responded with HTTP ${response.status}`), { retryable: false });
        lastError = new Error(`PlayFab gateway responded with HTTP ${response.status}`);
      } catch (error) {
        lastError = error;
        if (error.retryable === false) throw error;
      } finally {
        clearTimeout(timer);
      }
      await new Promise((resolve) => setTimeout(resolve, 200 * 2 ** attempt));
    }
    throw lastError || new Error('PlayFab gateway request failed');
  }

  async bootstrap() {
    if (this.cache && Date.now() < this.cacheUntil) return this.cache;
    const first = await this.request('/bootstrap');
    const combined = { ...first, events: [...(first.events || [])] };
    let cursor = first.nextCursor;
    let pages = 1;
    while (cursor && pages < 100) {
      const page = await this.request(`/bootstrap?cursor=${encodeURIComponent(cursor)}`);
      combined.events.push(...(page.events || []));
      cursor = page.nextCursor;
      pages++;
    }
    if (cursor) throw new Error('PlayFab gateway pagination exceeded 100 pages');
    this.cache = validateBootstrap(combined);
    this.cacheUntil = Date.now() + 15_000;
    return this.cache;
  }

  async authenticate(id, password) {
    const response = await this.request('/authenticate', {
      method: 'POST',
      body: { id: String(id || '').trim(), password: String(password || '') },
    });
    return response?.user || null;
  }

  async executePrivacyRequest(type, employeeId) {
    if (!['export', 'delete'].includes(type)) throw new Error('Unsupported privacy request');
    return this.request(`/privacy/${type}`, {
      method: 'POST',
      body: { employeeId: String(employeeId || '').trim() },
    });
  }

  async health() {
    try {
      const health = await this.request('/health');
      return {
        ok: health.ok !== false,
        provider: this.name,
        titleId: this.config.titleId,
        lastEventAt: health.lastEventAt || null,
        ingestionLagSeconds: health.ingestionLagSeconds ?? null,
      };
    }
    catch (error) { return { ok: false, provider: this.name, error: error.message }; }
  }
}
