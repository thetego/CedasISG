import { createHmac, randomBytes, timingSafeEqual } from "node:crypto";

const ROLE_PERMISSIONS = Object.freeze({
  super_admin: [
    "analytics:read:any",
    "employees:read",
    "reports:export",
    "settings:write",
    "integrations:manage",
    "users:manage",
    "audit:read:any",
    "privacy:request",
  ],
  admin: [
    "analytics:read:any",
    "employees:read",
    "reports:export",
    "integrations:manage",
    "users:manage",
    "audit:read:tenant",
    "privacy:request",
  ],
  manager: ["analytics:read:any", "employees:read", "reports:export", "privacy:request"],
  inspector: ["analytics:read:team", "employees:read", "reports:export", "privacy:request"],
  trainee: ["analytics:read:self", "privacy:request"],
});

function encode(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}
function signature(value, secret) {
  return createHmac("sha256", secret).update(value).digest("base64url");
}
function cookies(req) {
  return Object.fromEntries(
    (req.headers.cookie || "")
      .split(";")
      .filter(Boolean)
      .map((part) => {
        const index = part.indexOf("=");
        return [
          part.slice(0, index).trim(),
          decodeURIComponent(part.slice(index + 1)),
        ];
      }),
  );
}

export function createSession(user, secret, ttlSeconds = 28800) {
  const now = Math.floor(Date.now() / 1000);
  const payload = encode({
    sub: user.id,
    name: user.name,
    role: user.role,
    tenantId: user.tenantId || "tenant-cedas",
    teamId: user.teamId || null,
    permissions: ROLE_PERMISSIONS[user.role] || [],
    iat: now,
    exp: now + ttlSeconds,
    csrf: randomBytes(18).toString("base64url"),
  });
  return `${payload}.${signature(payload, secret)}`;
}

export function readSession(req, secret) {
  const jar = cookies(req);
  const token = jar["__Host-cedas.sid"] || jar.cedas_session;
  if (!token) return null;
  const [payload, sig] = token.split(".");
  if (!payload || !sig) return null;
  const expected = signature(payload, secret);
  if (
    sig.length !== expected.length ||
    !timingSafeEqual(Buffer.from(sig), Buffer.from(expected))
  )
    return null;
  try {
    const session = JSON.parse(Buffer.from(payload, "base64url").toString());
    return session.exp > Math.floor(Date.now() / 1000) ? session : null;
  } catch {
    return null;
  }
}

export function sessionCookie(token, secure, persistent = true) {
  const name = secure ? "__Host-cedas.sid" : "cedas_session";
  const lifetime = persistent ? "; Max-Age=28800" : "";
  return `${name}=${encodeURIComponent(token)}; Path=/; HttpOnly; SameSite=Lax${lifetime}${secure ? "; Secure" : ""}`;
}
export function clearCookie(secure) {
  const name = secure ? "__Host-cedas.sid" : "cedas_session";
  return `${name}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0${secure ? "; Secure" : ""}`;
}
export function can(session, permission) {
  return Boolean(session?.permissions?.includes(permission));
}
export function requirePermission(req, res, secret, permission, json) {
  const session = readSession(req, secret);
  if (!session) {
    json(res, 401, { error: "authentication_required" });
    return null;
  }
  if (!can(session, permission)) {
    json(res, 403, { error: "forbidden", required: permission });
    return null;
  }
  return session;
}
export function verifyCsrf(req, session) {
  return Boolean(session && req.headers["x-csrf-token"] === session.csrf);
}
export { ROLE_PERMISSIONS };
