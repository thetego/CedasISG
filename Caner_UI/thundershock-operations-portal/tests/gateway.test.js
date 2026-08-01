import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { loadGatewayConfig, parseGatewayUsers } from "../gateway/config.js";
import { GatewayStore } from "../gateway/store.js";
import { asyncBufferFromNodeBuffer } from "../gateway/blob-sync.js";

function actionEvent(eventId = "event-1") {
  return {
    eventId,
    schemaVersion: 2,
    eventType: "ActionCompleted",
    employeeId: "EMP-1",
    clientTimestamp: "2026-08-01T07:00:00.000Z",
    payload: {
      eventId,
      schemaVersion: 2,
      sessionId: "session-1",
      playerId: "EMP-1",
      role: "trainee",
      levelId: "level 1",
      sequenceId: "sequence-1",
      actionId: "action-1",
      type: "click",
      result: "success",
    },
  };
}

test("gateway configuration fails closed without a strong service token", () => {
  assert.throws(() => loadGatewayConfig({}, "."), /GATEWAY_SERVICE_TOKEN/);
  const config = loadGatewayConfig({ GATEWAY_SERVICE_TOKEN: "x".repeat(32) }, ".");
  assert.equal(config.titleId, "797DC");
  assert.equal(config.port, 4180);
});

test("gateway users reject malformed and short-password records", () => {
  assert.throws(() => parseGatewayUsers("{"), /valid JSON/);
  assert.deepEqual(parseGatewayUsers(JSON.stringify([
    { id: "EMP-1", name: "One", password: "short" },
    { id: "EMP-2", name: "Two", password: "correct-horse" },
  ])).map((user) => user.id), ["EMP-2"]);
});

test("parquet buffer adapter exposes correct random-access slices", async () => {
  const file = asyncBufferFromNodeBuffer(Buffer.from([10, 20, 30, 40, 50]));
  assert.equal(file.byteLength, 5);
  assert.deepEqual([...new Uint8Array(await file.slice(1, 4))], [20, 30, 40]);
  assert.deepEqual([...new Uint8Array(await file.slice(5, 5))], []);
});

test("gateway store hashes credentials, validates, deduplicates and pages events", async () => {
  const directory = await mkdtemp(join(tmpdir(), "cedas-gateway-"));
  const store = new GatewayStore(join(directory, "gateway.sqlite"));
  try {
    store.seedPeople([{
      id: "EMP-1",
      name: "Employee One",
      password: "correct-horse",
      role: "trainee",
      tenantId: "tenant-cedas",
      teamId: "field-a",
      department: "Training",
      location: "Sivas",
    }]);
    assert.equal(store.authenticate("EMP-1", "wrong"), null);
    assert.equal(store.authenticate("EMP-1", "correct-horse").teamId, "field-a");

    const result = store.ingest([
      actionEvent(),
      actionEvent(),
      { nope: true },
    ], "test");
    assert.deepEqual(result, { received: 3, accepted: 1, duplicate: 1, rejected: 1 });

    const bootstrap = store.bootstrap(0, 100);
    assert.equal(bootstrap.events.length, 1);
    assert.equal(bootstrap.events[0].payload.levelId, "level-1");
    assert.equal(bootstrap.total, 1);
    assert.equal(bootstrap.nextOffset, null);
    assert.equal(store.health().rejectedEventCount, 1);

    assert.equal(store.deleteEmployeeData("EMP-1"), 1);
    assert.equal(store.countEmployeeEvents("EMP-1"), 0);
    assert.equal(store.authenticate("EMP-1", "correct-horse"), null);
  } finally {
    store.close();
    await rm(directory, { recursive: true, force: true });
  }
});
