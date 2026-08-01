import { createDemoData } from './demo-data.js';
import { timingSafeEqual } from 'node:crypto';
import { validateBootstrap } from '../lib/event-schema.js';

export class DemoProvider {
  constructor(root, password = 'demo123') {
    this.root = root;
    this.name = 'demo';
    this.cache = null;
    this.password = password;
  }

  async bootstrap() {
    if (this.cache) return this.cache;
    this.cache = validateBootstrap(createDemoData());
    return this.cache;
  }

  async health() { return { ok: true, provider: this.name, mode: 'deterministic-demo' }; }

  async authenticate(id, password) {
    const supplied = Buffer.from(String(password || ''));
    const expected = Buffer.from(this.password);
    if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) return null;
    const data = await this.bootstrap();
    const people = [...data.managers, ...data.employees];
    return people.find((person) => person.id === String(id || '').trim()) || null;
  }

  async executePrivacyRequest(type, employeeId) {
    return { receiptId: `demo-${type}-${employeeId}`, status: 'simulated' };
  }
}
