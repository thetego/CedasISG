import test from "node:test";
import assert from "node:assert/strict";
import { loadConfig } from "../server/config.js";

test("demo provider works without external credentials", () => {
  const config = loadConfig({ DATA_PROVIDER: "demo" });
  assert.equal(config.provider, "demo");
  assert.equal(config.port, 4173);
});

test("playfab provider fails closed when credentials are missing", () => {
  assert.throws(
    () =>
      loadConfig({ DATA_PROVIDER: "playfab", SESSION_SECRET: "x".repeat(32) }),
    /missing/,
  );
});

test("playfab account settings are externalized", () => {
  const config = loadConfig({
    DATA_PROVIDER: "playfab",
    PLAYFAB_TITLE_ID: "ABCDE",
    PLAYFAB_DATA_URL: "https://gateway.example/",
    PLAYFAB_SERVICE_TOKEN: "t".repeat(32),
    SESSION_SECRET: "x".repeat(32),
  });
  assert.equal(config.playfab.titleId, "ABCDE");
  assert.equal(config.playfab.dataUrl, "https://gateway.example/");
});

test("playfab gateway URL rejects insecure non-loopback and embedded credentials", () => {
  const base = {
    DATA_PROVIDER: "playfab",
    PLAYFAB_TITLE_ID: "ABCDE",
    PLAYFAB_SERVICE_TOKEN: "t".repeat(32),
    SESSION_SECRET: "x".repeat(32),
  };
  assert.throws(() => loadConfig({ ...base, PLAYFAB_DATA_URL: "http://gateway.example/" }), /HTTPS/);
  assert.throws(() => loadConfig({ ...base, PLAYFAB_DATA_URL: "https://user:pass@gateway.example/" }), /HTTPS/);
  assert.equal(loadConfig({ ...base, PLAYFAB_DATA_URL: "http://127.0.0.1:4180/" }).provider, "playfab");
});

test("production refuses the built-in session secret", () => {
  assert.throws(
    () => loadConfig({ DATA_PROVIDER: "demo", NODE_ENV: "production" }),
    /SESSION_SECRET/,
  );
});
