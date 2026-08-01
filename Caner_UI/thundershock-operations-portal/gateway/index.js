import { createServer } from "node:http";
import { timingSafeEqual } from "node:crypto";
import { loadGatewayConfig, parseGatewayUsers } from "./config.js";
import { GatewayStore } from "./store.js";
import { BlobSync } from "./blob-sync.js";
import { PlayFabAdminClient } from "./playfab-admin.js";

const config = loadGatewayConfig();
const store = new GatewayStore(config.databasePath);
const users = parseGatewayUsers(config.usersJson);
if (users.length) store.seedPeople(users);
const playFab = new PlayFabAdminClient(
  config.titleId,
  config.playFabSecretKey,
  config.whitelistReaderId,
  config.whitelistKey,
);
const sync = new BlobSync(config.blobContainerUrl, store, config.retentionDays);

try { store.upsertRoster(await playFab.roster()); }
catch (error) { console.warn(JSON.stringify({ level: "warn", action: "roster.sync.failed", message: error.message })); }

function authorized(req) {
  const supplied = Buffer.from(String(req.headers.authorization || "").replace(/^Bearer\s+/i, ""));
  const expected = Buffer.from(config.serviceToken);
  return supplied.length === expected.length && timingSafeEqual(supplied, expected);
}

function response(res, status, body) {
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "DENY",
  });
  res.end(JSON.stringify(body));
}

async function readBody(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 64 * 1024) throw Object.assign(new Error("body_too_large"), { status: 413 });
    chunks.push(chunk);
  }
  if (!chunks.length) return {};
  try { return JSON.parse(Buffer.concat(chunks).toString("utf8")); }
  catch { throw Object.assign(new Error("invalid_json"), { status: 400 }); }
}

function cursorOffset(value) {
  if (!value) return 0;
  try {
    const offset = Number.parseInt(Buffer.from(value, "base64url").toString("utf8"), 10);
    return Number.isFinite(offset) && offset >= 0 ? offset : 0;
  } catch { return 0; }
}

async function runSync() {
  if (!config.blobContainerUrl) return;
  try {
    const result = await sync.sync();
    if (!result.skipped && result.blobs)
      console.log(JSON.stringify({ level: "info", action: "blob.sync.complete", ...result }));
  } catch (error) {
    console.error(JSON.stringify({ level: "error", action: "blob.sync.failed", message: error.message }));
  }
}

await runSync();
const syncTimer = setInterval(runSync, config.syncIntervalMs);
syncTimer.unref();

const server = createServer(async (req, res) => {
  const url = new URL(req.url || "/", `http://${config.host}:${config.port}`);
  try {
    if (!authorized(req)) return response(res, 401, { error: "unauthorized" });
    if (url.searchParams.get("titleId") !== config.titleId)
      return response(res, 404, { error: "title_not_found" });

    if (req.method === "GET" && url.pathname === "/health") {
      const health = store.health();
      const lag = health.lastEventAt
        ? Math.max(0, Math.round((Date.now() - new Date(health.lastEventAt).valueOf()) / 1000))
        : null;
      return response(res, config.blobContainerUrl && !sync.lastError ? 200 : 503, {
        ok: Boolean(config.blobContainerUrl) && !sync.lastError,
        ...health,
        lastSyncAt: sync.lastSyncAt,
        syncError: sync.lastError,
        ingestionLagSeconds: lag,
        privacyReady: Boolean(config.playFabSecretKey),
      });
    }
    if (req.method === "GET" && url.pathname === "/bootstrap") {
      const offset = cursorOffset(url.searchParams.get("cursor"));
      const data = store.bootstrap(offset, config.pageSize);
      return response(res, 200, {
        ...data,
        nextCursor: data.nextOffset === null
          ? null
          : Buffer.from(String(data.nextOffset)).toString("base64url"),
      });
    }
    if (req.method === "POST" && url.pathname === "/authenticate") {
      const body = await readBody(req);
      const user = store.authenticate(String(body.id || "").trim(), String(body.password || ""));
      return user ? response(res, 200, { user }) : response(res, 401, { error: "invalid_credentials" });
    }
    if (req.method === "POST" && ["/privacy/export", "/privacy/delete"].includes(url.pathname)) {
      const body = await readBody(req);
      const employeeId = String(body.employeeId || "").trim();
      if (!employeeId) return response(res, 400, { error: "employee_id_required" });
      const type = url.pathname.endsWith("export") ? "export" : "delete";
      const localEventCount = store.countEmployeeEvents(employeeId);
      const result = await playFab.privacy(type, employeeId);
      if (type === "delete") store.deleteEmployeeData(employeeId);
      return response(res, 200, { ...result, localEventCount });
    }
    if (req.method === "POST" && url.pathname === "/sync") {
      await runSync();
      return response(res, 200, { ok: !sync.lastError, lastSyncAt: sync.lastSyncAt });
    }
    return response(res, 404, { error: "not_found" });
  } catch (error) {
    console.error(JSON.stringify({ level: "error", path: url.pathname, message: error.message }));
    return response(res, error.status || 500, { error: "request_failed", message: error.message });
  }
});

server.requestTimeout = 30_000;
server.headersTimeout = 10_000;
server.listen(config.port, config.host, () => {
  console.log(`CEDAS PlayFab gateway listening on http://${config.host}:${config.port}`);
});

function shutdown() {
  clearInterval(syncTimer);
  server.close(() => { store.close(); process.exit(0); });
  setTimeout(() => process.exit(1), 10_000).unref();
}
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);
