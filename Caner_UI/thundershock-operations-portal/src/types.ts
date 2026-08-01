export type Employee = {
  id: string;
  name: string;
  role: string;
  department?: string;
  location?: string;
  teamId?: string;
};
export type EventType =
  | "LevelStarted"
  | "LevelCompleted"
  | "SequenceStarted"
  | "SequenceCompleted"
  | "ActionCompleted"
  | "QuizAnswered"
  | "QuizSummary"
  | "DragDropAttempt"
  | "MistakeRecorded"
  | "SurveyCompleted"
  | "SessionEnded";
export type EventPayload = {
  sessionId: string;
  playerId: string;
  role: string;
  schemaVersion: number;
  eventId: string;
  timestamp?: string;
  appVersion?: string;
  unityVersion?: string;
  levelId?: string;
  sourceLevelId?: string;
  sequenceId?: string;
  sequenceKey?: string;
  actionId?: string;
  actionIndex?: number;
  actionKey?: string;
  questionId?: string;
  questionKey?: string;
  displayName?: string;
  completed?: boolean;
  score?: number;
  timeSpent?: number;
  mistakes?: number;
  completionRate?: number;
  performanceRate?: number;
  type?: string;
  objectId?: string;
  result?: string;
  selectedAnswer?: string;
  correctAnswer?: string;
  isCorrect?: boolean;
  attempts?: number;
  attemptIndex?: number;
  totalQuestions?: number;
  correctAnswers?: number;
  wrongAnswers?: number;
  accuracy?: number;
  targetObject?: string;
  placements?: Array<{ item: string; droppedOn: string; correct: boolean }>;
  mistakeType?: string;
  severity?: 1 | 2 | 3;
  questionResults?: Array<{
    questionId?: string;
    questionText: string;
    selectedOptionIndex: number;
    isCorrect: boolean;
  }>;
  photoResults?: Array<{
    slotLabel: string;
    wasCaptured: boolean;
    isAligned: boolean;
    alignmentScore: number;
  }>;
  completionTime?: number;
  reason?: string;
  startTime?: string | number;
  endTime?: string | number;
  duration?: number;
};
export type EventRecord = {
  eventId: string;
  schemaVersion: number;
  eventType: EventType;
  employeeId: string;
  clientTimestamp: string;
  serverTimestamp?: string;
  payload: EventPayload;
};
export type Level = { emittedLevelId: string; name: string };
export type Bootstrap = {
  IS_MOCK: boolean;
  PROVIDER?: string;
  TODAY: string;
  employees: Employee[];
  managers: Employee[];
  events: EventRecord[];
  content: { levels: Level[] };
  quality?: {
    received: number;
    accepted: number;
    rejected: number;
    duplicate: number;
    invalidSamples?: Array<{ eventType: string; errors: string[] }>;
  } | null;
};
export type SessionUser = {
  id: string;
  name: string;
  role: string;
  tenantId: string;
  permissions: string[];
};
