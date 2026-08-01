import { createHash } from "node:crypto";

export const EVENT_TYPES = Object.freeze([
  "LevelStarted",
  "LevelCompleted",
  "SequenceStarted",
  "SequenceCompleted",
  "ActionCompleted",
  "QuizAnswered",
  "QuizSummary",
  "DragDropAttempt",
  "MistakeRecorded",
  "SurveyCompleted",
  "SessionEnded",
]);

const REQUIRED_PAYLOAD_FIELDS = Object.freeze({
  LevelStarted: ["levelId", "sessionId"],
  LevelCompleted: ["levelId", "sessionId", "completed", "score", "timeSpent", "mistakes"],
  SequenceStarted: ["levelId", "sequenceId", "sessionId"],
  SequenceCompleted: ["levelId", "sequenceId", "sessionId", "completed", "timeSpent"],
  ActionCompleted: ["levelId", "sequenceId", "actionId", "sessionId", "type", "result"],
  QuizAnswered: ["levelId", "sequenceId", "actionId", "questionId", "sessionId", "isCorrect"],
  QuizSummary: ["levelId", "sessionId", "totalQuestions", "correctAnswers", "wrongAnswers", "accuracy"],
  DragDropAttempt: ["levelId", "sequenceId", "actionId", "sessionId", "attempts", "placements"],
  MistakeRecorded: ["levelId", "sequenceId", "actionId", "sessionId", "mistakeType", "severity"],
  SurveyCompleted: ["levelId", "sequenceId", "actionId", "sessionId", "questionResults", "photoResults"],
  SessionEnded: ["levelId", "sessionId"],
});

const ALLOWED_ROLES = new Set(["super_admin", "admin", "manager", "inspector", "trainee"]);

function isRecord(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function parseObject(value) {
  if (isRecord(value)) return value;
  if (typeof value !== "string" || !value.trim()) return null;
  try {
    const parsed = JSON.parse(value);
    return isRecord(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function eventDocument(raw) {
  if (!isRecord(raw)) return null;
  if (raw.eventType && raw.payload) return raw;
  for (const candidate of [raw.Payload, raw.PayloadJSON, raw.EventData, raw.eventData, raw.Body, raw.body]) {
    const parsed = parseObject(candidate);
    if (parsed?.eventType && parsed?.payload) return parsed;
  }
  return raw;
}

function stableEventId(raw, document) {
  const supplied = document.eventId || raw.EventId || raw.eventId || raw.OriginalId || raw.originalId;
  if (typeof supplied === "string" && supplied.trim()) return supplied.trim();
  return createHash("sha256")
    .update(JSON.stringify(document))
    .digest("hex")
    .slice(0, 32);
}

export function canonicalLevelId(value) {
  const normalized = String(value || "")
    .trim()
    .toLocaleLowerCase("tr-TR")
    .replace(/[^\p{L}\p{N}]+/gu, "-")
    .replace(/^-+|-+$/g, "");
  if (["level-1", "level1", "lvl-1", "lvl1"].includes(normalized)) return "level-1";
  if (["level-2", "level2", "lvl-2", "lvl2"].includes(normalized)) return "level-2";
  if (["level-3", "level3", "lvl-3", "lvl3", "newlevel"].includes(normalized)) return "level-3";
  return normalized;
}

function timestamp(value) {
  const parsed = new Date(value || "");
  return Number.isNaN(parsed.valueOf()) ? null : parsed.toISOString();
}

function validateTypedFields(eventType, payload, errors) {
  if (["LevelCompleted", "SequenceCompleted"].includes(eventType) && typeof payload.completed !== "boolean")
    errors.push("payload.completed must be boolean");
  if (eventType === "QuizAnswered" && typeof payload.isCorrect !== "boolean")
    errors.push("payload.isCorrect must be boolean");
  if (eventType === "MistakeRecorded") {
    const severity = Number(payload.severity);
    if (!Number.isInteger(severity) || severity < 1 || severity > 3)
      errors.push("payload.severity must be an integer from 1 to 3");
    else payload.severity = severity;
  }
  if (eventType === "DragDropAttempt" && !Array.isArray(payload.placements))
    errors.push("payload.placements must be an array");
  if (eventType === "SurveyCompleted") {
    if (!Array.isArray(payload.questionResults)) errors.push("payload.questionResults must be an array");
    if (!Array.isArray(payload.photoResults)) errors.push("payload.photoResults must be an array");
  }
}

export function normalizeEvent(raw) {
  const errors = [];
  const document = eventDocument(raw);
  if (!document) return { ok: false, errors: ["event must be an object"] };

  const payload = isRecord(document.payload) ? { ...document.payload } : {};
  const eventType = String(
    document.eventType || raw.Name || raw.EventName || raw.FullName_Name || "",
  ).trim();
  if (!EVENT_TYPES.includes(eventType)) errors.push(`unsupported eventType '${eventType}'`);

  const clientTimestamp = timestamp(
    document.clientTimestamp || raw.OriginalTimestamp || raw.Timestamp || raw.timestamp,
  );
  if (!clientTimestamp) errors.push("clientTimestamp is invalid");

  const employeeId = String(
    document.employeeId || payload.playerId || raw.EntityLineage_title_player_account || "",
  ).trim();
  if (!employeeId) errors.push("employeeId is required");

  if (payload.levelId) payload.levelId = canonicalLevelId(payload.levelId);
  payload.playerId = String(payload.playerId || employeeId).trim();
  payload.role = ALLOWED_ROLES.has(payload.role) ? payload.role : "trainee";
  payload.schemaVersion = Number(payload.schemaVersion || document.schemaVersion || 1);
  payload.eventId = String(payload.eventId || document.eventId || stableEventId(raw, document));

  for (const field of REQUIRED_PAYLOAD_FIELDS[eventType] || []) {
    const value = payload[field];
    if (value === undefined || value === null || value === "") errors.push(`payload.${field} is required`);
  }
  validateTypedFields(eventType, payload, errors);

  return {
    ok: errors.length === 0,
    errors,
    event: {
      eventId: stableEventId(raw, document),
      schemaVersion: Number(document.schemaVersion || payload.schemaVersion || 1),
      eventType,
      employeeId,
      clientTimestamp,
      serverTimestamp: timestamp(raw.Timestamp || raw.serverTimestamp) || clientTimestamp,
      payload,
    },
  };
}

function sanitizePeople(people) {
  const seen = new Set();
  return people.flatMap((person) => {
    if (!isRecord(person)) return [];
    const id = String(person.id || person.playerId || "").trim();
    const name = String(person.name || person.displayName || "").trim();
    if (!id || !name || seen.has(id.toLocaleLowerCase("tr-TR"))) return [];
    seen.add(id.toLocaleLowerCase("tr-TR"));
    return [{
      id,
      name,
      role: ALLOWED_ROLES.has(person.role) ? person.role : "trainee",
      tenantId: String(person.tenantId || "tenant-cedas"),
      department: person.department ? String(person.department) : undefined,
      location: person.location ? String(person.location) : undefined,
      teamId: person.teamId ? String(person.teamId) : undefined,
    }];
  });
}

export function validateBootstrap(payload) {
  if (!isRecord(payload)) throw new Error("PlayFab gateway returned an invalid document");
  for (const field of ["events", "employees", "managers"])
    if (!Array.isArray(payload[field])) throw new Error(`PlayFab gateway field '${field}' must be an array`);
  if (!isRecord(payload.content) || !Array.isArray(payload.content.levels))
    throw new Error("PlayFab gateway field 'content.levels' is required");

  const invalidEvents = [];
  const seenIds = new Set();
  const events = [];
  for (const raw of payload.events) {
    const normalized = normalizeEvent(raw);
    if (!normalized.ok) {
      invalidEvents.push({ eventType: normalized.event?.eventType || "unknown", errors: normalized.errors });
      continue;
    }
    if (seenIds.has(normalized.event.eventId)) continue;
    seenIds.add(normalized.event.eventId);
    events.push(normalized.event);
  }
  events.sort((left, right) => left.clientTimestamp.localeCompare(right.clientTimestamp));

  const levels = payload.content.levels.map((level) => ({
    ...level,
    emittedLevelId: canonicalLevelId(level.emittedLevelId || level.levelId),
  }));

  return {
    ...payload,
    content: { ...payload.content, levels },
    employees: sanitizePeople(payload.employees),
    managers: sanitizePeople(payload.managers),
    events,
    quality: {
      received: payload.events.length,
      accepted: events.length,
      rejected: invalidEvents.length,
      duplicate: payload.events.length - events.length - invalidEvents.length,
      invalidSamples: invalidEvents.slice(0, 20),
    },
  };
}
