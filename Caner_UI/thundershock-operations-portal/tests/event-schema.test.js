import test from "node:test";
import assert from "node:assert/strict";
import { EVENT_TYPES, normalizeEvent, validateBootstrap } from "../server/lib/event-schema.js";
import { DemoProvider } from "../server/providers/demo.js";

function baseEvent(eventType, payload = {}) {
  return {
    eventId: `${eventType}-1`,
    schemaVersion: 2,
    eventType,
    clientTimestamp: "2026-08-01T07:00:00.000Z",
    employeeId: "EMP-1",
    payload: {
      sessionId: "session-1",
      playerId: "EMP-1",
      role: "trainee",
      schemaVersion: 2,
      eventId: `${eventType}-1`,
      levelId: "level 1",
      ...payload,
    },
  };
}

test("normalizer accepts new PlayFab PayloadJSON envelopes", () => {
  const event = baseEvent("ActionCompleted", {
    sequenceId: "seq-1",
    actionId: "action-1",
    type: "camera_move",
    result: "success",
  });
  const result = normalizeEvent({
    Name: "ActionCompleted",
    OriginalId: event.eventId,
    OriginalTimestamp: event.clientTimestamp,
    PayloadJSON: JSON.stringify(event),
  });
  assert.equal(result.ok, true);
  assert.equal(result.event.payload.levelId, "level-1");
  assert.equal(result.event.eventId, event.eventId);
});

test("normalizer rejects unsupported types and invalid severity", () => {
  const unsupported = normalizeEvent(baseEvent("UnknownEvent"));
  assert.equal(unsupported.ok, false);
  const mistake = normalizeEvent(baseEvent("MistakeRecorded", {
    sequenceId: "seq-1",
    actionId: "action-1",
    mistakeType: "wrong_answer",
    severity: 9,
  }));
  assert.equal(mistake.ok, false);
  assert.match(mistake.errors.join(" "), /severity/);
});

test("bootstrap validates, sorts and deduplicates all eleven event types", () => {
  const payloads = {
    LevelStarted: {},
    LevelCompleted: { completed: true, score: 90, timeSpent: 20, mistakes: 1 },
    SequenceStarted: { sequenceId: "seq-1" },
    SequenceCompleted: { sequenceId: "seq-1", completed: true, timeSpent: 10 },
    ActionCompleted: { sequenceId: "seq-1", actionId: "a-1", type: "click", result: "success" },
    QuizAnswered: { sequenceId: "seq-1", actionId: "a-1", questionId: "q-1", isCorrect: true },
    QuizSummary: { totalQuestions: 1, correctAnswers: 1, wrongAnswers: 0, accuracy: 1 },
    DragDropAttempt: { sequenceId: "seq-1", actionId: "a-1", attempts: 1, placements: [] },
    MistakeRecorded: { sequenceId: "seq-1", actionId: "a-1", mistakeType: "wrong_drop", severity: 2 },
    SurveyCompleted: { sequenceId: "seq-1", actionId: "a-1", questionResults: [], photoResults: [] },
    SessionEnded: {},
  };
  const events = EVENT_TYPES.map((type, index) => ({
    ...baseEvent(type, payloads[type]),
    eventId: `${type}-${index}`,
    clientTimestamp: `2026-08-01T07:${String(index).padStart(2, "0")}:00.000Z`,
    payload: { ...baseEvent(type, payloads[type]).payload, eventId: `${type}-${index}` },
  }));
  const result = validateBootstrap({
    content: { levels: [{ emittedLevelId: "NewLevel", name: "Seviye 3" }] },
    employees: [{ id: "EMP-1", name: "Test", role: "trainee" }],
    managers: [],
    events: [...events.reverse(), events[0]],
  });
  assert.equal(result.events.length, 11);
  assert.equal(result.quality.duplicate, 1);
  assert.equal(result.content.levels[0].emittedLevelId, "level-3");
  assert.deepEqual(new Set(result.events.map((event) => event.eventType)), new Set(EVENT_TYPES));
});

test("demo authentication requires the exact configured password", async () => {
  const provider = new DemoProvider(".", "demo123");
  assert.ok(await provider.authenticate("ADMIN_DEMO", "demo123"));
  assert.equal(await provider.authenticate("ADMIN_DEMO", "wrong"), null);
  assert.equal(await provider.authenticate("ADMIN_DEMO", ""), null);
});
