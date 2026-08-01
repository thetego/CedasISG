import test from "node:test";
import assert from "node:assert/strict";
import {
  createSession,
  readSession,
  can,
  ROLE_PERMISSIONS,
  sessionCookie,
} from "../server/lib/auth.js";

const secret = "x".repeat(64);
function requestFor(token) {
  return { headers: { cookie: `__Host-cedas.sid=${token}` } };
}

test("signed HttpOnly session preserves manager permissions", () => {
  const token = createSession(
    { id: "ADMIN_DEMO", name: "Demo", role: "manager" },
    secret,
  );
  const session = readSession(requestFor(token), secret);
  assert.equal(session.sub, "ADMIN_DEMO");
  assert.equal(can(session, "analytics:read:any"), true);
  assert.equal(can(session, "settings:write"), false);
  assert.ok(session.csrf.length >= 20);
});

test("employee is self-scoped and cannot export organization data", () => {
  const token = createSession(
    { id: "EMP-1042", name: "Ahmet", role: "trainee" },
    secret,
  );
  const session = readSession(requestFor(token), secret);
  assert.equal(can(session, "analytics:read:self"), true);
  assert.equal(can(session, "analytics:read:any"), false);
  assert.equal(can(session, "reports:export"), false);
});

test("tampered session is rejected", () => {
  const token = createSession(
    { id: "EMP-1042", name: "Ahmet", role: "trainee" },
    secret,
  );
  assert.equal(readSession(requestFor(token + "x"), secret), null);
});

test("permission matrix is deny by default", () => {
  assert.deepEqual(ROLE_PERMISSIONS.unknown, undefined);
  assert.equal(can({ permissions: [] }, "audit:read"), false);
});

test("super admin and customer admin have distinct scopes", () => {
  const superSession = readSession(
    requestFor(
      createSession(
        {
          id: "SUPER_ADMIN",
          name: "Platform",
          role: "super_admin",
          tenantId: "platform",
        },
        secret,
      ),
    ),
    secret,
  );
  const adminSession = readSession(
    requestFor(
      createSession(
        {
          id: "ADMIN_DEMO",
          name: "Customer",
          role: "admin",
          tenantId: "tenant-cedas",
        },
        secret,
      ),
    ),
    secret,
  );
  assert.equal(can(superSession, "audit:read:any"), true);
  assert.equal(can(adminSession, "audit:read:any"), false);
  assert.equal(can(adminSession, "audit:read:tenant"), true);
  assert.equal(adminSession.tenantId, "tenant-cedas");
});

test("remember preference controls cookie persistence", () => {
  const persistent = sessionCookie("signed-token", true, true);
  const browserSession = sessionCookie("signed-token", true, false);
  assert.match(persistent, /Max-Age=28800/);
  assert.doesNotMatch(browserSession, /Max-Age=/);
  assert.match(browserSession, /HttpOnly; SameSite=Lax; Secure/);
});
