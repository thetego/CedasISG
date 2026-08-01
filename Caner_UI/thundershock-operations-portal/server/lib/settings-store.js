import {
  createCipheriv,
  createDecipheriv,
  createHash,
  randomBytes,
} from "node:crypto";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { dirname } from "node:path";

function key(secret) {
  return createHash("sha256").update(secret).digest();
}
function encrypt(value, secret) {
  const iv = randomBytes(12),
    cipher = createCipheriv("aes-256-gcm", key(secret), iv);
  const encrypted = Buffer.concat([
    cipher.update(JSON.stringify(value), "utf8"),
    cipher.final(),
  ]);
  return JSON.stringify({
    v: 1,
    iv: iv.toString("base64"),
    tag: cipher.getAuthTag().toString("base64"),
    data: encrypted.toString("base64"),
  });
}
function decrypt(document, secret) {
  const parsed = JSON.parse(document),
    decipher = createDecipheriv(
      "aes-256-gcm",
      key(secret),
      Buffer.from(parsed.iv, "base64"),
    );
  decipher.setAuthTag(Buffer.from(parsed.tag, "base64"));
  return JSON.parse(
    Buffer.concat([
      decipher.update(Buffer.from(parsed.data, "base64")),
      decipher.final(),
    ]).toString("utf8"),
  );
}
export class SettingsStore {
  constructor(path, secret) {
    this.path = path;
    this.secret = secret;
  }
  async load() {
    try {
      return decrypt(await readFile(this.path, "utf8"), this.secret);
    } catch (error) {
      if (error.code === "ENOENT") return { tenants: {} };
      throw error;
    }
  }
  async save(settings) {
    await mkdir(dirname(this.path), { recursive: true });
    const temporary = `${this.path}.${process.pid}.tmp`;
    await writeFile(temporary, encrypt(settings, this.secret), { mode: 0o600 });
    await rename(temporary, this.path);
  }
  async getTenant(tenantId) {
    const all = await this.load();
    return all.tenants?.[tenantId] || null;
  }
  async setTenant(tenantId, value) {
    const all = await this.load();
    all.tenants = all.tenants || {};
    all.tenants[tenantId] = { ...value, updatedAt: new Date().toISOString() };
    await this.save(all);
    return all.tenants[tenantId];
  }
}
export function publicSettings(value) {
  return value
    ? {
        provider: value.provider || "demo",
        titleId: value.titleId || "",
        dataUrl: value.dataUrl || "",
        hasServiceToken: Boolean(value.serviceToken),
        serviceTokenMasked: value.serviceToken
          ? `••••••••••••${value.serviceToken.slice(-4)}`
          : "",
        updatedAt: value.updatedAt || null,
      }
    : {
        provider: "demo",
        titleId: "",
        dataUrl: "",
        hasServiceToken: false,
        serviceTokenMasked: "",
        updatedAt: null,
      };
}
