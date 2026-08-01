import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { PrivacyRequestStore } from "../server/lib/privacy-store.js";

test("privacy requests are encrypted, scoped and receipt-tracked", async () => {
  const directory = await mkdtemp(join(tmpdir(), "cedas-privacy-"));
  try {
    const path = join(directory, "requests.enc");
    const store = new PrivacyRequestStore(path, "a".repeat(64));
    const request = await store.create({
      employeeId: "EMP-1",
      tenantId: "tenant-1",
      type: "export",
      requestedBy: "EMP-1",
    });
    const raw = await readFile(path, "utf8");
    assert.equal(raw.includes("EMP-1"), false);
    assert.equal((await store.list({ employeeId: "EMP-2", tenantId: "tenant-1", readAny: false })).length, 0);
    assert.equal((await store.list({ employeeId: "ADMIN", tenantId: "tenant-1", readAny: true })).length, 1);

    const processing = await store.markProcessing(request.id, "tenant-1", "ADMIN");
    assert.equal(processing.status, "processing");
    const completed = await store.complete(request.id, "tenant-1", { jobReceiptId: "receipt-1" });
    assert.equal(completed.status, "completed");
    assert.equal(completed.receiptId, "receipt-1");
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
