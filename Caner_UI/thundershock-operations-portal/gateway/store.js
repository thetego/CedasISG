import Database from "better-sqlite3";
import { randomBytes, scryptSync, timingSafeEqual } from "node:crypto";
import { mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { normalizeEvent } from "../server/lib/event-schema.js";

const MANAGER_ROLES = new Set(["super_admin", "admin", "manager"]);

function passwordHash(password, salt) {
  return scryptSync(password, salt, 64);
}

export class GatewayStore {
  constructor(path) {
    mkdirSync(dirname(path), { recursive: true });
    this.db = new Database(path);
    this.db.pragma("journal_mode = WAL");
    this.db.pragma("foreign_keys = ON");
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS events (
        event_id TEXT PRIMARY KEY,
        event_type TEXT NOT NULL,
        employee_id TEXT NOT NULL,
        client_timestamp TEXT NOT NULL,
        server_timestamp TEXT NOT NULL,
        event_json TEXT NOT NULL,
        ingested_at TEXT NOT NULL
      );
      CREATE INDEX IF NOT EXISTS events_time_idx ON events(client_timestamp, event_id);
      CREATE INDEX IF NOT EXISTS events_employee_idx ON events(employee_id, client_timestamp);
      CREATE TABLE IF NOT EXISTS people (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        role TEXT NOT NULL,
        tenant_id TEXT NOT NULL,
        team_id TEXT,
        department TEXT,
        location TEXT,
        password_salt BLOB,
        password_hash BLOB,
        updated_at TEXT NOT NULL
      );
      CREATE TABLE IF NOT EXISTS processed_blobs (
        name TEXT PRIMARY KEY,
        etag TEXT,
        rows INTEGER NOT NULL,
        processed_at TEXT NOT NULL
      );
      CREATE TABLE IF NOT EXISTS rejected_events (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        source TEXT NOT NULL,
        errors TEXT NOT NULL,
        sample TEXT,
        rejected_at TEXT NOT NULL
      );
    `);
    this.insertEvent = this.db.prepare(`
      INSERT OR IGNORE INTO events
      (event_id, event_type, employee_id, client_timestamp, server_timestamp, event_json, ingested_at)
      VALUES (@eventId, @eventType, @employeeId, @clientTimestamp, @serverTimestamp, @eventJson, @ingestedAt)
    `);
    this.insertRejected = this.db.prepare(
      "INSERT INTO rejected_events(source, errors, sample, rejected_at) VALUES (?, ?, ?, ?)",
    );
  }

  seedPeople(users) {
    const statement = this.db.prepare(`
      INSERT INTO people(id, name, role, tenant_id, team_id, department, location, password_salt, password_hash, updated_at)
      VALUES (@id, @name, @role, @tenantId, @teamId, @department, @location, @salt, @hash, @updatedAt)
      ON CONFLICT(id) DO UPDATE SET
        name=excluded.name, role=excluded.role, tenant_id=excluded.tenant_id,
        team_id=excluded.team_id, department=excluded.department, location=excluded.location,
        password_salt=excluded.password_salt, password_hash=excluded.password_hash,
        updated_at=excluded.updated_at
    `);
    const transaction = this.db.transaction((items) => {
      for (const user of items) {
        const salt = randomBytes(16);
        statement.run({
          ...user,
          salt,
          hash: passwordHash(user.password, salt),
          updatedAt: new Date().toISOString(),
        });
      }
    });
    transaction(users);
  }

  upsertRoster(players) {
    const statement = this.db.prepare(`
      INSERT INTO people(id, name, role, tenant_id, team_id, department, location, updated_at)
      VALUES (@id, @name, @role, @tenantId, @teamId, @department, @location, @updatedAt)
      ON CONFLICT(id) DO UPDATE SET
        name=excluded.name, role=CASE WHEN people.password_hash IS NULL THEN excluded.role ELSE people.role END,
        department=COALESCE(excluded.department, people.department),
        location=COALESCE(excluded.location, people.location), updated_at=excluded.updated_at
    `);
    const transaction = this.db.transaction((items) => {
      for (const player of items) statement.run({
        id: String(player.playerId || player.id).trim(),
        name: String(player.displayName || player.name).trim(),
        role: ["manager", "inspector", "admin", "trainee"].includes(player.role) ? player.role : "trainee",
        tenantId: String(player.tenantId || "tenant-cedas"),
        teamId: player.teamId || null,
        department: player.department || null,
        location: player.location || null,
        updatedAt: new Date().toISOString(),
      });
    });
    transaction(players.filter((player) => (player.playerId || player.id) && (player.displayName || player.name)));
  }

  authenticate(id, password) {
    const person = this.db.prepare("SELECT * FROM people WHERE id = ?").get(id);
    if (!person?.password_hash || !person?.password_salt) return null;
    const actual = passwordHash(password, person.password_salt);
    if (actual.length !== person.password_hash.length || !timingSafeEqual(actual, person.password_hash)) return null;
    return this.person(person);
  }

  person(row) {
    return {
      id: row.id,
      name: row.name,
      role: row.role,
      tenantId: row.tenant_id,
      teamId: row.team_id || undefined,
      department: row.department || undefined,
      location: row.location || undefined,
    };
  }

  ingest(rows, source = "unknown") {
    const result = { received: rows.length, accepted: 0, duplicate: 0, rejected: 0 };
    const transaction = this.db.transaction((items) => {
      for (const row of items) {
        const normalized = normalizeEvent(row);
        if (!normalized.ok) {
          result.rejected++;
          this.insertRejected.run(
            source,
            JSON.stringify(normalized.errors),
            JSON.stringify(row).slice(0, 4000),
            new Date().toISOString(),
          );
          continue;
        }
        const info = this.insertEvent.run({
          ...normalized.event,
          eventJson: JSON.stringify(normalized.event),
          ingestedAt: new Date().toISOString(),
        });
        if (info.changes) result.accepted++;
        else result.duplicate++;
      }
    });
    transaction(rows);
    return result;
  }

  isBlobProcessed(name, etag) {
    const row = this.db.prepare("SELECT etag FROM processed_blobs WHERE name = ?").get(name);
    return Boolean(row && row.etag === etag);
  }

  markBlobProcessed(name, etag, rows) {
    this.db.prepare(`
      INSERT INTO processed_blobs(name, etag, rows, processed_at) VALUES (?, ?, ?, ?)
      ON CONFLICT(name) DO UPDATE SET etag=excluded.etag, rows=excluded.rows, processed_at=excluded.processed_at
    `).run(name, etag || "", rows, new Date().toISOString());
  }

  prune(retentionDays) {
    const cutoff = new Date(Date.now() - retentionDays * 86_400_000).toISOString();
    return this.db.prepare("DELETE FROM events WHERE client_timestamp < ?").run(cutoff).changes;
  }

  bootstrap(offset, limit) {
    const events = this.db.prepare(
      "SELECT event_json FROM events ORDER BY client_timestamp, event_id LIMIT ? OFFSET ?",
    ).all(limit, offset).map((row) => JSON.parse(row.event_json));
    const people = this.db.prepare("SELECT * FROM people ORDER BY name").all().map((row) => this.person(row));
    const total = this.db.prepare("SELECT COUNT(*) AS count FROM events").get().count;
    return {
      TODAY: new Date().toISOString(),
      content: { levels: [
        { emittedLevelId: "level-1", name: "Seviye 1 · Aydınlatma Direği" },
        { emittedLevelId: "level-2", name: "Seviye 2 · Trafo Köşkü" },
        { emittedLevelId: "level-3", name: "Seviye 3 · Trafo Merkezi" },
      ] },
      quizBank: {},
      employees: people.filter((person) => !MANAGER_ROLES.has(person.role)),
      managers: people.filter((person) => MANAGER_ROLES.has(person.role)),
      events,
      total,
      nextOffset: offset + events.length < total ? offset + events.length : null,
    };
  }

  health() {
    const events = this.db.prepare("SELECT COUNT(*) AS count, MAX(client_timestamp) AS last FROM events").get();
    const rejected = this.db.prepare("SELECT COUNT(*) AS count FROM rejected_events").get().count;
    return { eventCount: events.count, lastEventAt: events.last || null, rejectedEventCount: rejected };
  }

  countEmployeeEvents(employeeId) {
    return this.db.prepare("SELECT COUNT(*) AS count FROM events WHERE employee_id = ?").get(employeeId).count;
  }

  deleteEmployeeData(employeeId) {
    const transaction = this.db.transaction(() => {
      const events = this.db.prepare("DELETE FROM events WHERE employee_id = ?").run(employeeId).changes;
      this.db.prepare("DELETE FROM people WHERE id = ?").run(employeeId);
      return events;
    });
    return transaction();
  }

  close() { this.db.close(); }
}
