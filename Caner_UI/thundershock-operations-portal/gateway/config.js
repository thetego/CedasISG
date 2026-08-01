import { resolve } from "node:path";

function integer(value, fallback) {
  const parsed = Number.parseInt(value || "", 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

export function loadGatewayConfig(env = process.env, root = process.cwd()) {
  const config = {
    host: env.GATEWAY_HOST || "127.0.0.1",
    port: integer(env.GATEWAY_PORT, 4180),
    titleId: env.PLAYFAB_TITLE_ID || "797DC",
    serviceToken: env.GATEWAY_SERVICE_TOKEN || "",
    playFabSecretKey: env.PLAYFAB_SECRET_KEY || "",
    blobContainerUrl: env.AZURE_BLOB_CONTAINER_URL || "",
    databasePath: resolve(root, env.GATEWAY_DATABASE_FILE || ".data/gateway.sqlite"),
    syncIntervalMs: integer(env.GATEWAY_SYNC_INTERVAL_MS, 60_000),
    retentionDays: integer(env.GATEWAY_RETENTION_DAYS, 365),
    pageSize: Math.min(1000, Math.max(100, integer(env.GATEWAY_PAGE_SIZE, 1000))),
    usersJson: env.GATEWAY_USERS_JSON || "[]",
    whitelistReaderId: env.PLAYFAB_WHITELIST_READER_ID || "whitelist_reader",
    whitelistKey: env.PLAYFAB_WHITELIST_KEY || "PlayerWhitelist",
  };
  if (!/^[A-Za-z0-9]+$/.test(config.titleId)) throw new Error("PLAYFAB_TITLE_ID is invalid");
  if (config.serviceToken.length < 32) throw new Error("GATEWAY_SERVICE_TOKEN must contain at least 32 characters");
  if (config.syncIntervalMs < 10_000) throw new Error("GATEWAY_SYNC_INTERVAL_MS must be at least 10000");
  if (config.retentionDays < 1) throw new Error("GATEWAY_RETENTION_DAYS must be positive");
  return Object.freeze(config);
}

export function parseGatewayUsers(value) {
  let users;
  try { users = JSON.parse(value); }
  catch { throw new Error("GATEWAY_USERS_JSON must be valid JSON"); }
  if (!Array.isArray(users)) throw new Error("GATEWAY_USERS_JSON must be an array");
  return users.map((user) => ({
    id: String(user.id || "").trim(),
    name: String(user.name || "").trim(),
    password: String(user.password || ""),
    role: String(user.role || "trainee"),
    tenantId: String(user.tenantId || "tenant-cedas"),
    teamId: user.teamId ? String(user.teamId) : null,
    department: user.department ? String(user.department) : null,
    location: user.location ? String(user.location) : null,
  })).filter((user) => user.id && user.name && user.password.length >= 8);
}
