import { createServer } from "node:http";
import { randomUUID } from "node:crypto";
import { appendFile, mkdir, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { loadConfig, validGatewayUrl } from "./config.js";
import { createProvider } from "./providers/index.js";
import { json, securityHeaders, staticFile } from "./lib/http.js";
import {
  clearCookie,
  createSession,
  readSession,
  requirePermission,
  sessionCookie,
  verifyCsrf,
} from "./lib/auth.js";
import { readJson } from "./lib/http.js";
import { SettingsStore, publicSettings } from "./lib/settings-store.js";
import { PrivacyRequestStore } from "./lib/privacy-store.js";

const serverDir = dirname(fileURLToPath(import.meta.url));
const root = resolve(serverDir, "..");
const publicRoot = resolve(root, "dist");
const config = loadConfig();
const provider = createProvider(config, root);
const tenantProviders = new Map();
const settingsStore = new SettingsStore(
  resolve(root, config.settingsFile),
  config.sessionSecret,
);
const privacyStore = new PrivacyRequestStore(
  resolve(root, config.privacyFile),
  config.sessionSecret,
);
const savedSettings = await settingsStore.load();
for (const [tenantId, value] of Object.entries(savedSettings.tenants || {})) {
  if (
    value.provider === "playfab" &&
    value.titleId &&
    value.dataUrl &&
    value.serviceToken &&
    value.serviceToken.length >= 32 &&
    /^[A-Za-z0-9]+$/.test(value.titleId) &&
    validGatewayUrl(value.dataUrl)
  ) {
    tenantProviders.set(
      tenantId,
      createProvider(
        {
          ...config,
          provider: "playfab",
          playfab: {
            titleId: value.titleId,
            dataUrl: value.dataUrl,
            serviceToken: value.serviceToken,
            timeoutMs: config.playfab.timeoutMs,
          },
        },
        root,
      ),
    );
  }
}
const startedAt = Date.now();
const auditPath = resolve(root, config.auditFile);
let auditLog = [];
try {
  auditLog = (await readFile(auditPath, "utf8"))
    .trim()
    .split("\n")
    .filter(Boolean)
    .slice(-500)
    .reverse()
    .map((line) => JSON.parse(line));
} catch (error) {
  if (error.code !== "ENOENT")
    console.error("Audit log load failed", error.message);
}
const loginAttempts = new Map();
function audit(action, subject, req, detail = {}) {
  const entry = {
    at: new Date().toISOString(),
    action,
    subject,
    ip: req.socket.remoteAddress,
    requestId: req.headers["x-request-id"],
    ...detail,
  };
  auditLog.unshift(entry);
  if (auditLog.length > 500) auditLog.length = 500;
  mkdir(dirname(auditPath), { recursive: true })
    .then(() =>
      appendFile(auditPath, JSON.stringify(entry) + "\n", { mode: 0o600 }),
    )
    .catch((error) => console.error("Audit append failed", error.message));
}
function loginAllowed(ip) {
  const now = Date.now(),
    state = loginAttempts.get(ip) || { count: 0, reset: now + 60000 };
  if (now > state.reset) {
    state.count = 0;
    state.reset = now + 60000;
  }
  state.count++;
  loginAttempts.set(ip, state);
  return state.count <= 10;
}
function validOrigin(req) {
  const origin = req.headers.origin;
  return !origin || origin === config.origin;
}

function safeBootstrap(data, activeProvider = provider) {
  return {
    IS_MOCK: activeProvider.name === "demo",
    PROVIDER: activeProvider.name,
    TODAY:
      data.TODAY instanceof Date
        ? data.TODAY.toISOString()
        : data.TODAY || new Date().toISOString(),
    content: data.content,
    quizBank: data.quizBank || {},
    employees: data.employees,
    managers: data.managers,
    events: data.events,
    quality: data.quality || null,
  };
}

async function authenticateAcrossProviders(id, password) {
  const candidates = [[null, provider], ...tenantProviders.entries()];
  const visited = new Set();
  for (const [tenantId, candidate] of candidates) {
    if (visited.has(candidate)) continue;
    visited.add(candidate);
    try {
      const person = await candidate.authenticate(id, password);
      if (person) return {
        person: { ...person, tenantId: person.tenantId || tenantId || "tenant-cedas" },
        activeProvider: candidate,
      };
    } catch (error) {
      console.warn(JSON.stringify({ level: "warn", action: "auth.provider.failed", provider: candidate.name, message: error.message }));
    }
  }
  return null;
}

const server = createServer(async (req, res) => {
  const requestId = randomUUID();
  securityHeaders(res, requestId);
  const url = new URL(req.url || "/", config.origin);
  try {
    if (req.method === "GET" && url.pathname === "/health/live") {
      return json(res, 200, {
        status: "ok",
        uptimeSeconds: Math.floor((Date.now() - startedAt) / 1000),
        requestId,
      });
    }
    if (req.method === "GET" && url.pathname === "/health/ready") {
      const health = await provider.health();
      return json(res, health.ok ? 200 : 503, {
        status: health.ok ? "ready" : "unavailable",
        ...health,
        requestId,
      });
    }
    if (req.method === "GET" && url.pathname === "/api/v1/runtime") {
      return json(res, 200, {
        provider: provider.name,
        demo: provider.name === "demo",
      });
    }
    if (req.method === "POST" && url.pathname === "/api/v1/auth/login") {
      if (!validOrigin(req))
        return json(res, 403, { error: "origin_rejected" });
      const ip = req.socket.remoteAddress || "unknown";
      if (!loginAllowed(ip))
        return json(res, 429, { error: "too_many_attempts" });
      const body = await readJson(req);
      const authenticated = await authenticateAcrossProviders(
        String(body.id || "").trim(),
        String(body.password || ""),
      );
      if (!authenticated) {
        audit("auth.login.failed", String(body.id || ""), req);
        return json(res, 401, { error: "invalid_credentials" });
      }
      const { person } = authenticated;
      const role = [
        "super_admin",
        "admin",
        "manager",
        "inspector",
        "trainee",
      ].includes(person.role)
        ? person.role
        : "trainee";
      const token = createSession({ ...person, role }, config.sessionSecret);
      const session = readSession(
        { headers: { cookie: `__Host-cedas.sid=${token}` } },
        config.sessionSecret,
      );
      audit("auth.login.success", person.id, req, {
        role,
        tenantId: person.tenantId || "tenant-cedas",
      });
      return json(
        res,
        200,
        {
          user: {
            id: person.id,
            name: person.name,
            role,
            tenantId: person.tenantId || "tenant-cedas",
            permissions: session.permissions,
          },
          csrfToken: session.csrf,
        },
        {
          "Set-Cookie": sessionCookie(
            token,
            config.origin.startsWith("https://"),
            body.remember !== false,
          ),
        },
      );
    }
    if (req.method === "GET" && url.pathname === "/api/v1/auth/me") {
      const session = readSession(req, config.sessionSecret);
      return session
        ? json(res, 200, {
            user: {
              id: session.sub,
              name: session.name,
              role: session.role,
              tenantId: session.tenantId,
              permissions: session.permissions,
            },
            csrfToken: session.csrf,
          })
        : json(res, 401, { error: "authentication_required" });
    }
    if (req.method === "POST" && url.pathname === "/api/v1/auth/logout") {
      if (!validOrigin(req))
        return json(res, 403, { error: "origin_rejected" });
      const session = readSession(req, config.sessionSecret);
      if (!session || !verifyCsrf(req, session))
        return json(res, 403, { error: "csrf_failed" });
      audit("auth.logout", session.sub, req);
      return json(
        res,
        200,
        { ok: true },
        { "Set-Cookie": clearCookie(config.origin.startsWith("https://")) },
      );
    }
    if (req.method === "GET" && url.pathname === "/api/v1/bootstrap") {
      const anySession = readSession(req, config.sessionSecret);
      if (!anySession)
        return json(res, 401, { error: "authentication_required" });
      const activeProvider =
        tenantProviders.get(anySession.tenantId) || provider;
      const data = safeBootstrap(
        await activeProvider.bootstrap(),
        activeProvider,
      );
      if (anySession.permissions.includes("analytics:read:any"))
        return json(res, 200, {
          ...data,
          managers:
            anySession.role === "super_admin"
              ? data.managers
              : data.managers.filter(
                  (manager) =>
                    (manager.tenantId || "tenant-cedas") ===
                    anySession.tenantId,
                ),
        });
      if (anySession.permissions.includes("analytics:read:team")) {
        const employees = anySession.teamId
          ? data.employees.filter((employee) => employee.teamId === anySession.teamId)
          : data.employees.filter((employee) => employee.id === anySession.sub);
        const employeeIds = new Set(employees.map((employee) => employee.id));
        return json(res, 200, {
          ...data,
          employees,
          managers: [],
          events: data.events.filter((event) => employeeIds.has(event.employeeId)),
        });
      }
      if (anySession.permissions.includes("analytics:read:self"))
        return json(res, 200, {
          ...data,
          employees: data.employees.filter((e) => e.id === anySession.sub),
          managers: [],
          events: data.events.filter((e) => e.employeeId === anySession.sub),
        });
      return json(res, 403, { error: "forbidden" });
    }
    if (req.method === "GET" && url.pathname === "/api/v1/audit") {
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      const readAny = session.permissions.includes("audit:read:any"),
        readTenant = session.permissions.includes("audit:read:tenant");
      if (!readAny && !readTenant)
        return json(res, 403, { error: "forbidden" });
      const items = readAny
        ? auditLog
        : auditLog.filter((item) => item.tenantId === session.tenantId);
      return json(res, 200, { items });
    }
    if (req.method === "GET" && url.pathname === "/api/v1/privacy/requests") {
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      const readAny = session.permissions.includes("users:manage");
      const items = await privacyStore.list({
        employeeId: session.sub,
        tenantId: session.tenantId,
        readAny,
      });
      return json(res, 200, { items });
    }
    if (req.method === "POST" && url.pathname === "/api/v1/privacy/requests") {
      if (!validOrigin(req)) return json(res, 403, { error: "origin_rejected" });
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      if (!verifyCsrf(req, session)) return json(res, 403, { error: "csrf_failed" });
      const body = await readJson(req);
      const type = String(body.type || "");
      if (!['export', 'delete'].includes(type)) return json(res, 400, { error: "invalid_request_type" });
      const canManage = session.permissions.includes("users:manage");
      const employeeId = canManage && body.employeeId
        ? String(body.employeeId).trim()
        : session.sub;
      if (!employeeId) return json(res, 400, { error: "employee_id_required" });
      const request = await privacyStore.create({
        employeeId,
        tenantId: session.tenantId,
        type,
        requestedBy: session.sub,
      });
      audit("privacy.request.created", request.id, req, {
        tenantId: session.tenantId,
        employeeId,
        requestType: type,
      });
      return json(res, 201, { request });
    }
    const privacyExecute = url.pathname.match(/^\/api\/v1\/privacy\/requests\/([^/]+)\/execute$/);
    if (req.method === "POST" && privacyExecute) {
      if (!validOrigin(req)) return json(res, 403, { error: "origin_rejected" });
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      if (!session.permissions.includes("users:manage")) return json(res, 403, { error: "forbidden" });
      if (!verifyCsrf(req, session)) return json(res, 403, { error: "csrf_failed" });
      const request = await privacyStore.markProcessing(
        decodeURIComponent(privacyExecute[1]),
        session.tenantId,
        session.sub,
      );
      if (!request) return json(res, 404, { error: "privacy_request_not_pending" });
      const activeProvider = tenantProviders.get(session.tenantId) || provider;
      try {
        const result = await activeProvider.executePrivacyRequest(request.type, request.employeeId);
        const completed = await privacyStore.complete(request.id, session.tenantId, result || {});
        audit("privacy.request.completed", request.id, req, {
          tenantId: session.tenantId,
          employeeId: request.employeeId,
          requestType: request.type,
          receiptId: completed.receiptId,
        });
        return json(res, 200, { request: completed });
      } catch (error) {
        await privacyStore.fail(request.id, session.tenantId, error.message);
        audit("privacy.request.failed", request.id, req, {
          tenantId: session.tenantId,
          employeeId: request.employeeId,
          requestType: request.type,
        });
        return json(res, 502, { error: "privacy_operation_failed", message: error.message });
      }
    }
    if (
      req.method === "GET" &&
      url.pathname === "/api/v1/integrations/playfab"
    ) {
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      if (!session.permissions.includes("integrations:manage"))
        return json(res, 403, { error: "forbidden" });
      const requested = url.searchParams.get("tenantId"),
        tenantId =
          session.role === "super_admin" && requested
            ? requested
            : session.tenantId;
      return json(res, 200, {
        tenantId,
        settings: publicSettings(await settingsStore.getTenant(tenantId)),
      });
    }
    if (
      req.method === "PUT" &&
      url.pathname === "/api/v1/integrations/playfab"
    ) {
      if (!validOrigin(req))
        return json(res, 403, { error: "origin_rejected" });
      const session = readSession(req, config.sessionSecret);
      if (!session) return json(res, 401, { error: "authentication_required" });
      if (!session.permissions.includes("integrations:manage"))
        return json(res, 403, { error: "forbidden" });
      if (!verifyCsrf(req, session))
        return json(res, 403, { error: "csrf_failed" });
      const body = await readJson(req),
        requested = String(body.tenantId || ""),
        tenantId =
          session.role === "super_admin" && requested
            ? requested
            : session.tenantId;
      if (!["demo", "playfab"].includes(body.provider))
        return json(res, 400, { error: "invalid_provider" });
      const previous = await settingsStore.getTenant(tenantId);
      const next = {
        provider: body.provider,
        titleId: String(body.titleId || "").trim(),
        dataUrl: String(body.dataUrl || "").trim(),
        serviceToken:
          String(body.serviceToken || "").trim() ||
          previous?.serviceToken ||
          "",
      };
      if (
        next.provider === "playfab" &&
        (!next.titleId || !next.dataUrl || !next.serviceToken)
      )
        return json(res, 400, { error: "missing_playfab_fields" });
      let candidate = provider;
      if (next.provider === "playfab") {
        if (!/^[A-Za-z0-9]+$/.test(next.titleId))
          return json(res, 400, { error: "invalid_title_id" });
        if (next.serviceToken.length < 32)
          return json(res, 400, { error: "weak_service_token" });
        if (!validGatewayUrl(next.dataUrl)) {
          return json(res, 400, { error: "invalid_data_url" });
        }
        candidate = createProvider(
          {
            ...config,
            provider: "playfab",
            playfab: { ...next, timeoutMs: config.playfab.timeoutMs },
          },
          root,
        );
        const health = await candidate.health();
        if (!health.ok)
          return json(res, 422, {
            error: "connection_test_failed",
            message: health.error,
          });
      }
      await settingsStore.setTenant(tenantId, next);
      if (next.provider === "playfab") tenantProviders.set(tenantId, candidate);
      else tenantProviders.delete(tenantId);
      audit("integration.playfab.updated", tenantId, req, {
        tenantId,
        actor: session.sub,
        provider: next.provider,
      });
      return json(res, 200, {
        tenantId,
        settings: publicSettings(await settingsStore.getTenant(tenantId)),
      });
    }
    if (req.method !== "GET" && req.method !== "HEAD")
      return json(res, 405, { error: "method_not_allowed", requestId });
    if (await staticFile(res, publicRoot, url.pathname)) return;
    if (
      !url.pathname.startsWith("/api/") &&
      !url.pathname.startsWith("/health/")
    ) {
      if (await staticFile(res, publicRoot, "/index.html")) return;
    }
    return json(res, 404, { error: "not_found", requestId });
  } catch (error) {
    console.error(
      JSON.stringify({
        level: "error",
        requestId,
        path: url.pathname,
        message: error.message,
      }),
    );
    return json(res, error.status || 500, {
      error: "request_failed",
      message: error.message,
      requestId,
    });
  }
});

server.requestTimeout = 20_000;
server.headersTimeout = 10_000;
server.keepAliveTimeout = 5_000;
server.listen(config.port, config.host, () => {
  console.log(
    `Thundershock portal listening on ${config.origin} [provider=${provider.name}]`,
  );
});

function shutdown(signal) {
  console.log(`${signal} received; closing HTTP server`);
  server.close((error) => process.exit(error ? 1 : 0));
  setTimeout(() => process.exit(1), 10_000).unref();
}
process.on("SIGTERM", () => shutdown("SIGTERM"));
process.on("SIGINT", () => shutdown("SIGINT"));
