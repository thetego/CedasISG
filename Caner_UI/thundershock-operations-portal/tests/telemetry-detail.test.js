import test from "node:test";
import assert from "node:assert/strict";
import { createDemoData } from "../server/providers/demo-data.js";
import { validateBootstrap } from "../server/lib/event-schema.js";
import {
  buildEmployeeDetail,
  buildScenarioDetail,
  buildTrainingSessions,
} from "../src/lib/telemetry-detail.ts";

function demoBootstrap() {
  const data = validateBootstrap(createDemoData());
  return {
    ...data,
    IS_MOCK: true,
    PROVIDER: "demo",
    TODAY:
      data.TODAY instanceof Date ? data.TODAY.toISOString() : data.TODAY,
  };
}

test("employee drilldown reconstructs completed and interrupted sessions", () => {
  const data = demoBootstrap();
  const employee = data.employees.find((item) => item.id === "TEST001");
  const detail = buildEmployeeDetail(data, employee);
  assert.equal(detail.totals.sessions, 8);
  assert.equal(detail.totals.completed, 7);
  assert.equal(detail.totals.interrupted, 1);
  assert.equal(detail.totals.quizAnswers, detail.totals.correctAnswers + detail.totals.mistakes);
  assert.equal(detail.sessions.every((session) => session.events.length > 0), true);
});

test("scenario drilldown aggregates employees, sequences and events without fabricated rows", () => {
  const data = demoBootstrap();
  const levelId = data.content.levels[0].emittedLevelId;
  const detail = buildScenarioDetail(data, levelId);
  assert.equal(detail.summary.levelId, levelId);
  assert.equal(detail.summary.sessions, detail.sessions.length);
  assert.equal(detail.summary.employees, detail.employees.length);
  assert.ok(detail.sequences.some((sequence) => sequence.sequenceId === "SEQ-MAIN"));
  assert.equal(detail.events.every((event) => event.payload.levelId === levelId), true);
});

test("stale session without terminal event is marked incomplete with data-quality warning", () => {
  const data = demoBootstrap();
  const event = {
    ...data.events[0],
    eventId: "stale-action",
    eventType: "ActionCompleted",
    clientTimestamp: "2026-07-20T06:00:00.000Z",
    payload: {
      ...data.events[0].payload,
      eventId: "stale-action",
      sessionId: "stale-session",
      actionId: "A-1",
      actionKey: "level-1:SEQ-1:A-1",
      sequenceId: "SEQ-1",
      levelId: "level-1",
      type: "click",
      result: "success",
    },
  };
  const [session] = buildTrainingSessions(data, [event]);
  assert.equal(session.status, "incomplete");
  assert.deepEqual(session.warnings, [
    "LevelStarted olayı eksik",
    "Terminal olay eksik",
  ]);
});

test("mistake detail links the preceding quiz answer by stable action key", () => {
  const data = demoBootstrap();
  const employee = data.employees[0];
  const base = {
    schemaVersion: 2,
    employeeId: employee.id,
    payload: {
      sessionId: "answer-link-session",
      playerId: employee.id,
      role: employee.role,
      schemaVersion: 2,
      levelId: "level-1",
      sequenceId: "SEQ-1",
      actionId: "QUIZ-1",
      actionKey: "level-1:SEQ-1:QUIZ-1",
    },
  };
  const quiz = {
    ...base,
    eventId: "answer-link-quiz",
    eventType: "QuizAnswered",
    clientTimestamp: "2026-07-20T06:01:00.000Z",
    payload: {
      ...base.payload,
      eventId: "answer-link-quiz",
      questionId: "Q-SAFETY-1",
      selectedAnswer: "Enerji var",
      correctAnswer: "Enerji yok",
      isCorrect: false,
    },
  };
  const mistake = {
    ...base,
    eventId: "answer-link-mistake",
    eventType: "MistakeRecorded",
    clientTimestamp: "2026-07-20T06:01:01.000Z",
    payload: {
      ...base.payload,
      eventId: "answer-link-mistake",
      mistakeType: "wrong_answer",
      severity: 3,
    },
  };
  const detail = buildEmployeeDetail(
    { ...data, events: [quiz, mistake] },
    employee,
  );
  assert.equal(detail.mistakes[0].selectedAnswer, "Enerji var");
  assert.equal(detail.mistakes[0].correctAnswer, "Enerji yok");
  assert.equal(detail.mistakes[0].severity, 3);
});
