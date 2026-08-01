import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, readFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { SettingsStore, publicSettings } from "../server/lib/settings-store.js";

test("integration secrets are encrypted at rest and masked in API view", async () => {
  const dir = await mkdtemp(join(tmpdir(), "cedas-settings-")),
    path = join(dir, "settings.enc");
  const store = new SettingsStore(path, "s".repeat(64));
  await store.setTenant("tenant-cedas", {
    provider: "playfab",
    titleId: "ABCDE",
    dataUrl: "https://gateway.example/",
    serviceToken: "super-secret-token",
  });
  const raw = await readFile(path, "utf8");
  assert.equal(raw.includes("super-secret-token"), false);
  const value = await store.getTenant("tenant-cedas"),
    view = publicSettings(value);
  assert.equal(value.serviceToken, "super-secret-token");
  assert.equal(view.hasServiceToken, true);
  assert.equal("serviceToken" in view, false);
  assert.match(view.serviceTokenMasked, /oken$/);
});
