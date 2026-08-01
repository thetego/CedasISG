import type { Bootstrap, Employee, EventRecord, EventType } from "../types";

export type SessionStatus = "completed" | "interrupted" | "active" | "incomplete";

export type TrainingSession = {
  id: string;
  employeeId: string;
  levelId: string;
  levelName: string;
  startedAt: string;
  endedAt: string;
  status: SessionStatus;
  durationSeconds: number;
  score?: number;
  mistakes: number;
  criticalMistakes: number;
  actions: number;
  quizAnswers: number;
  correctAnswers: number;
  accuracy?: number;
  sequenceIds: string[];
  eventTypes: string[];
  warnings: string[];
  events: EventRecord[];
};

export type MistakeDetail = {
  event: EventRecord;
  sessionId: string;
  levelId: string;
  levelName: string;
  sequenceId: string;
  actionId: string;
  actionKey: string;
  mistakeType: string;
  severity: number;
  selectedAnswer?: string;
  correctAnswer?: string;
  questionId?: string;
};

export type ScenarioDetail = {
  levelId: string;
  levelName: string;
  sessions: number;
  completed: number;
  interrupted: number;
  incomplete: number;
  employees: number;
  actions: number;
  mistakes: number;
  criticalMistakes: number;
  quizAnswers: number;
  correctAnswers: number;
  accuracy?: number;
  averageDurationSeconds?: number;
  sequenceIds: string[];
};

export type EmployeeDetail = {
  employee: Employee;
  events: EventRecord[];
  sessions: TrainingSession[];
  mistakes: MistakeDetail[];
  scenarios: ScenarioDetail[];
  eventTypeCounts: Record<string, number>;
  totals: {
    sessions: number;
    completed: number;
    interrupted: number;
    active: number;
    incomplete: number;
    actions: number;
    mistakes: number;
    criticalMistakes: number;
    quizAnswers: number;
    correctAnswers: number;
    accuracy?: number;
    durationSeconds: number;
  };
};

const TERMINAL_TYPES = new Set<EventType>(["LevelCompleted", "SessionEnded"]);

function validTime(value: string | undefined) {
  const parsed = new Date(value || "").valueOf();
  return Number.isFinite(parsed) ? parsed : 0;
}

function levelName(data: Bootstrap, levelId: string) {
  return data.content.levels.find((level) => level.emittedLevelId === levelId)?.name || levelId || "Bilinmeyen senaryo";
}

function numeric(value: unknown, fallback = 0) {
  const result = Number(value);
  return Number.isFinite(result) ? result : fallback;
}

function lastEventOfType(events: EventRecord[], type: EventType) {
  return [...events].reverse().find((event) => event.eventType === type);
}

export function buildTrainingSessions(
  data: Bootstrap,
  events: EventRecord[],
  referenceTime = new Date(data.TODAY).valueOf(),
): TrainingSession[] {
  const groups = new Map<string, EventRecord[]>();
  for (const event of events) {
    const sessionId = String(event.payload.sessionId || "").trim();
    if (!sessionId) continue;
    const list = groups.get(sessionId) || [];
    list.push(event);
    groups.set(sessionId, list);
  }

  return [...groups.entries()].map(([id, rawEvents]) => {
    const ordered = [...rawEvents].sort((left, right) =>
      left.clientTimestamp.localeCompare(right.clientTimestamp),
    );
    const start = ordered.find((event) => event.eventType === "LevelStarted");
    const completed = lastEventOfType(ordered, "LevelCompleted");
    const ended = lastEventOfType(ordered, "SessionEnded");
    const first = ordered[0];
    const last = ordered.at(-1)!;
    const levelId = String(start?.payload.levelId || first.payload.levelId || "");
    const latestTimestamp = validTime(last.clientTimestamp);
    const status: SessionStatus = completed
      ? "completed"
      : ended
        ? "interrupted"
        : referenceTime - latestTimestamp <= 2 * 60 * 60 * 1000
          ? "active"
          : "incomplete";
    const terminal = completed || ended;
    const mistakes = ordered.filter((event) => event.eventType === "MistakeRecorded");
    const quizzes = ordered.filter((event) => event.eventType === "QuizAnswered");
    const correctAnswers = quizzes.filter((event) => event.payload.isCorrect === true).length;
    const elapsed = Math.max(0, Math.round((validTime(last.clientTimestamp) - validTime(first.clientTimestamp)) / 1000));
    const durationSeconds = Math.max(0, Math.round(numeric(terminal?.payload.timeSpent, elapsed)));
    const warnings: string[] = [];
    if (!start) warnings.push("LevelStarted olayı eksik");
    if (!terminal && status !== "active") warnings.push("Terminal olay eksik");
    if (ordered.some((event) => !event.payload.levelId)) warnings.push("levelId eksik event var");
    if (ordered.some((event) => event.eventType === "ActionCompleted" && !event.payload.actionKey))
      warnings.push("Legacy actionKey eksikliği var");

    return {
      id,
      employeeId: first.employeeId,
      levelId,
      levelName: levelName(data, levelId),
      startedAt: start?.clientTimestamp || first.clientTimestamp,
      endedAt: terminal?.clientTimestamp || last.clientTimestamp,
      status,
      durationSeconds,
      score: completed?.payload.score === undefined ? undefined : numeric(completed.payload.score),
      mistakes: mistakes.length,
      criticalMistakes: mistakes.filter((event) => numeric(event.payload.severity) === 3).length,
      actions: ordered.filter((event) => event.eventType === "ActionCompleted").length,
      quizAnswers: quizzes.length,
      correctAnswers,
      accuracy: quizzes.length ? (correctAnswers / quizzes.length) * 100 : undefined,
      sequenceIds: [...new Set(ordered.map((event) => String(event.payload.sequenceId || "")).filter(Boolean))],
      eventTypes: [...new Set(ordered.map((event) => event.eventType))],
      warnings,
      events: ordered,
    };
  }).sort((left, right) => right.startedAt.localeCompare(left.startedAt));
}

function mistakeDetails(data: Bootstrap, events: EventRecord[]): MistakeDetail[] {
  const quizBySessionAction = new Map<string, EventRecord[]>();
  for (const event of events) {
    if (event.eventType !== "QuizAnswered") continue;
    const key = `${event.payload.sessionId}|${event.payload.actionKey || event.payload.actionId || ""}`;
    const list = quizBySessionAction.get(key) || [];
    list.push(event);
    quizBySessionAction.set(key, list);
  }
  return events.filter((event) => event.eventType === "MistakeRecorded").map((event) => {
    const action = String(event.payload.actionKey || event.payload.actionId || "");
    const key = `${event.payload.sessionId}|${action}`;
    const candidates = quizBySessionAction.get(key) || [];
    const quiz = candidates
      .filter((candidate) => candidate.clientTimestamp <= event.clientTimestamp)
      .at(-1) || candidates[0];
    const levelId = String(event.payload.levelId || "");
    return {
      event,
      sessionId: String(event.payload.sessionId || ""),
      levelId,
      levelName: levelName(data, levelId),
      sequenceId: String(event.payload.sequenceId || "—"),
      actionId: String(event.payload.actionId || "—"),
      actionKey: String(event.payload.actionKey || event.payload.actionId || "—"),
      mistakeType: String(event.payload.mistakeType || "unknown"),
      severity: numeric(event.payload.severity, 1),
      selectedAnswer: quiz?.payload.selectedAnswer,
      correctAnswer: quiz?.payload.correctAnswer,
      questionId: quiz?.payload.questionId,
    };
  }).sort((left, right) => right.event.clientTimestamp.localeCompare(left.event.clientTimestamp));
}

function scenarioDetails(data: Bootstrap, sessions: TrainingSession[]): ScenarioDetail[] {
  const knownIds = new Set(data.content.levels.map((level) => level.emittedLevelId));
  sessions.forEach((session) => knownIds.add(session.levelId));
  return [...knownIds].filter(Boolean).map((levelId) => {
    const scoped = sessions.filter((session) => session.levelId === levelId);
    const quizAnswers = scoped.reduce((total, session) => total + session.quizAnswers, 0);
    const correctAnswers = scoped.reduce((total, session) => total + session.correctAnswers, 0);
    const completedDurations = scoped.filter((session) => session.status === "completed").map((session) => session.durationSeconds);
    return {
      levelId,
      levelName: levelName(data, levelId),
      sessions: scoped.length,
      completed: scoped.filter((session) => session.status === "completed").length,
      interrupted: scoped.filter((session) => session.status === "interrupted").length,
      incomplete: scoped.filter((session) => ["incomplete", "active"].includes(session.status)).length,
      employees: new Set(scoped.map((session) => session.employeeId)).size,
      actions: scoped.reduce((total, session) => total + session.actions, 0),
      mistakes: scoped.reduce((total, session) => total + session.mistakes, 0),
      criticalMistakes: scoped.reduce((total, session) => total + session.criticalMistakes, 0),
      quizAnswers,
      correctAnswers,
      accuracy: quizAnswers ? (correctAnswers / quizAnswers) * 100 : undefined,
      averageDurationSeconds: completedDurations.length
        ? completedDurations.reduce((total, value) => total + value, 0) / completedDurations.length
        : undefined,
      sequenceIds: [...new Set(scoped.flatMap((session) => session.sequenceIds))],
    };
  }).sort((left, right) => right.sessions - left.sessions || left.levelId.localeCompare(right.levelId));
}

export function buildEmployeeDetail(data: Bootstrap, employee: Employee): EmployeeDetail {
  const events = data.events
    .filter((event) => event.employeeId === employee.id)
    .sort((left, right) => left.clientTimestamp.localeCompare(right.clientTimestamp));
  const sessions = buildTrainingSessions(data, events);
  const eventTypeCounts: Record<string, number> = {};
  events.forEach((event) => { eventTypeCounts[event.eventType] = (eventTypeCounts[event.eventType] || 0) + 1; });
  const quizAnswers = sessions.reduce((total, session) => total + session.quizAnswers, 0);
  const correctAnswers = sessions.reduce((total, session) => total + session.correctAnswers, 0);
  return {
    employee,
    events,
    sessions,
    mistakes: mistakeDetails(data, events),
    scenarios: scenarioDetails(data, sessions),
    eventTypeCounts,
    totals: {
      sessions: sessions.length,
      completed: sessions.filter((session) => session.status === "completed").length,
      interrupted: sessions.filter((session) => session.status === "interrupted").length,
      active: sessions.filter((session) => session.status === "active").length,
      incomplete: sessions.filter((session) => session.status === "incomplete").length,
      actions: sessions.reduce((total, session) => total + session.actions, 0),
      mistakes: sessions.reduce((total, session) => total + session.mistakes, 0),
      criticalMistakes: sessions.reduce((total, session) => total + session.criticalMistakes, 0),
      quizAnswers,
      correctAnswers,
      accuracy: quizAnswers ? (correctAnswers / quizAnswers) * 100 : undefined,
      durationSeconds: sessions.reduce((total, session) => total + session.durationSeconds, 0),
    },
  };
}

export function buildScenarioDetail(data: Bootstrap, levelId: string) {
  const allSessions = buildTrainingSessions(data, data.events);
  const summary = scenarioDetails(data, allSessions).find((scenario) => scenario.levelId === levelId);
  const sessions = allSessions.filter((session) => session.levelId === levelId);
  const events = sessions.flatMap((session) => session.events)
    .sort((left, right) => right.clientTimestamp.localeCompare(left.clientTimestamp));
  const employees = data.employees.map((employee) => {
    const scoped = sessions.filter((session) => session.employeeId === employee.id);
    const answers = scoped.reduce((total, session) => total + session.quizAnswers, 0);
    const correct = scoped.reduce((total, session) => total + session.correctAnswers, 0);
    return {
      employee,
      sessions: scoped.length,
      completed: scoped.filter((session) => session.status === "completed").length,
      mistakes: scoped.reduce((total, session) => total + session.mistakes, 0),
      actions: scoped.reduce((total, session) => total + session.actions, 0),
      accuracy: answers ? (correct / answers) * 100 : undefined,
      lastAt: scoped[0]?.startedAt,
    };
  }).filter((item) => item.sessions > 0).sort((left, right) => right.mistakes - left.mistakes || right.sessions - left.sessions);
  const sequenceMap = new Map<string, EventRecord[]>();
  events.forEach((event) => {
    const sequenceId = String(event.payload.sequenceId || "");
    if (!sequenceId) return;
    const list = sequenceMap.get(sequenceId) || [];
    list.push(event);
    sequenceMap.set(sequenceId, list);
  });
  const sequences = [...sequenceMap.entries()].map(([sequenceId, scoped]) => ({
    sequenceId,
    events: scoped.length,
    actions: scoped.filter((event) => event.eventType === "ActionCompleted").length,
    mistakes: scoped.filter((event) => event.eventType === "MistakeRecorded").length,
    quizAnswers: scoped.filter((event) => event.eventType === "QuizAnswered").length,
    employees: new Set(scoped.map((event) => event.employeeId)).size,
  })).sort((left, right) => right.mistakes - left.mistakes || left.sequenceId.localeCompare(right.sequenceId));
  return { summary, sessions, events, employees, sequences, mistakes: mistakeDetails(data, events) };
}

export function eventTitle(event: EventRecord) {
  const labels: Record<EventType, string> = {
    LevelStarted: "Senaryo başladı",
    LevelCompleted: "Senaryo tamamlandı",
    SequenceStarted: "Sekans başladı",
    SequenceCompleted: "Sekans tamamlandı",
    ActionCompleted: "Aksiyon tamamlandı",
    QuizAnswered: event.payload.isCorrect ? "Quiz doğru yanıtlandı" : "Quiz yanlış yanıtlandı",
    QuizSummary: "Quiz özeti",
    DragDropAttempt: "Sürükle-bırak denemesi",
    MistakeRecorded: "Hata kaydedildi",
    SurveyCompleted: "Saha anketi tamamlandı",
    SessionEnded: "Oturum sonlandı",
  };
  return labels[event.eventType];
}
