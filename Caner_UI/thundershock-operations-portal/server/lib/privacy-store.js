import { createCipheriv, createDecipheriv, createHash, randomBytes, randomUUID } from "node:crypto";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { dirname } from "node:path";

function encryptionKey(secret) {
  return createHash("sha256").update(`privacy:${secret}`).digest();
}

function encrypt(value, secret) {
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", encryptionKey(secret), iv);
  const data = Buffer.concat([cipher.update(JSON.stringify(value), "utf8"), cipher.final()]);
  return JSON.stringify({
    v: 1,
    iv: iv.toString("base64"),
    tag: cipher.getAuthTag().toString("base64"),
    data: data.toString("base64"),
  });
}

function decrypt(document, secret) {
  const parsed = JSON.parse(document);
  const decipher = createDecipheriv(
    "aes-256-gcm",
    encryptionKey(secret),
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

export class PrivacyRequestStore {
  constructor(path, secret) {
    this.path = path;
    this.secret = secret;
    this.queue = Promise.resolve();
  }

  async load() {
    try {
      const result = decrypt(await readFile(this.path, "utf8"), this.secret);
      return Array.isArray(result.requests) ? result : { requests: [] };
    } catch (error) {
      if (error.code === "ENOENT") return { requests: [] };
      throw error;
    }
  }

  async save(document) {
    await mkdir(dirname(this.path), { recursive: true });
    const temporary = `${this.path}.${process.pid}.tmp`;
    await writeFile(temporary, encrypt(document, this.secret), { mode: 0o600 });
    await rename(temporary, this.path);
  }

  async mutate(operation) {
    const run = this.queue.then(async () => {
      const document = await this.load();
      const result = await operation(document.requests);
      await this.save(document);
      return result;
    });
    this.queue = run.catch(() => undefined);
    return run;
  }

  async create({ employeeId, tenantId, type, requestedBy }) {
    return this.mutate(async (requests) => {
      const existing = requests.find((request) =>
        request.employeeId === employeeId && request.tenantId === tenantId &&
        request.type === type && ["pending", "processing"].includes(request.status));
      if (existing) return existing;
      const request = {
        id: randomUUID(),
        employeeId,
        tenantId,
        type,
        status: "pending",
        requestedBy,
        requestedAt: new Date().toISOString(),
      };
      requests.push(request);
      return request;
    });
  }

  async list({ employeeId, tenantId, readAny }) {
    const { requests } = await this.load();
    return requests
      .filter((request) => request.tenantId === tenantId && (readAny || request.employeeId === employeeId))
      .sort((left, right) => right.requestedAt.localeCompare(left.requestedAt));
  }

  async markProcessing(id, tenantId, actor) {
    return this.mutate(async (requests) => {
      const request = requests.find((item) => item.id === id && item.tenantId === tenantId);
      if (!request || request.status !== "pending") return null;
      Object.assign(request, { status: "processing", processedBy: actor, processingAt: new Date().toISOString() });
      return { ...request };
    });
  }

  async complete(id, tenantId, result) {
    return this.mutate(async (requests) => {
      const request = requests.find((item) => item.id === id && item.tenantId === tenantId);
      if (!request) return null;
      Object.assign(request, {
        status: "completed",
        completedAt: new Date().toISOString(),
        receiptId: result.receiptId || result.jobReceiptId || null,
        resultStatus: result.status || "accepted",
      });
      return { ...request };
    });
  }

  async fail(id, tenantId, message) {
    return this.mutate(async (requests) => {
      const request = requests.find((item) => item.id === id && item.tenantId === tenantId);
      if (!request) return null;
      Object.assign(request, { status: "failed", failedAt: new Date().toISOString(), failure: message });
      return { ...request };
    });
  }
}
