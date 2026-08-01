const names = [
  "Ahmet Yılmaz",
  "Elif Demir",
  "Mehmet Kaya",
  "Zeynep Şahin",
  "Can Arslan",
  "Selin Koç",
  "Murat Aydın",
  "Derya Çelik",
  "Emre Yıldız",
  "Burcu Aksoy",
  "Okan Kurt",
  "İrem Güneş",
];
const levels = [
  { emittedLevelId: "level-1", name: "Direk & Trafo Köşkü", sequences: [] },
  { emittedLevelId: "level-2", name: "Hücre ve Pano Odası", sequences: [] },
  { emittedLevelId: "level-3", name: "AG Trafo Dairesi", sequences: [] },
];
function rng(seed) {
  let value = seed >>> 0;
  return () =>
    (value = (Math.imul(value, 1664525) + 1013904223) >>> 0) / 4294967296;
}
export function createDemoData() {
  const random = rng(42);
  const employees = names.map((name, i) => ({
    id: `EMP-${String(1042 + i).padStart(4, "0")}`,
    name,
    role: i % 4 === 0 ? "inspector" : "trainee",
    department: ["Saha Operasyon", "Bakım", "İSG", "Dağıtım"][i % 4],
    location: ["Konya", "Karaman", "Aksaray"][i % 3],
  }));
  employees.unshift({
    id: "TEST001",
    name: "Demo Çalışan",
    role: "trainee",
    department: "Saha Operasyon",
    location: "Konya",
  });
  const events = [];
  const today = new Date("2026-07-29T09:00:00.000Z");
  employees.forEach((employee, employeeIndex) => {
    const runCount =
      employee.id === "TEST001" ? 8 : 2 + Math.floor(random() * 6);
    for (let run = 0; run < runCount; run++) {
      const level = levels[(employeeIndex + run) % levels.length];
      const started = new Date(
        today.getTime() - (run * 4 + employeeIndex) * 86400000,
      );
      const sessionId = `${employee.id}-${run + 1}`;
      const envelope = (eventType, offset, payload) => ({
        eventType,
        employeeId: employee.id,
        clientTimestamp: new Date(
          started.getTime() + offset * 60000,
        ).toISOString(),
        payload: {
          sessionId,
          playerId: employee.id,
          role: employee.role,
          levelId: level.emittedLevelId,
          ...payload,
        },
      });
      events.push(envelope("LevelStarted", 0, { displayName: employee.name }));
      events.push(envelope("SequenceStarted", 1, {
        sequenceId: "SEQ-MAIN",
        startTime: started.toISOString(),
      }));
      events.push(envelope("DragDropAttempt", 2, {
        sequenceId: "SEQ-MAIN",
        actionId: "PPE-DROP",
        targetObject: "equipment-slot",
        attempts: 1,
        placements: [{ item: "insulated-glove", droppedOn: "equipment-slot", correct: true }],
      }));
      if (run % 2 === 0) {
        events.push(envelope("SurveyCompleted", 3, {
          sequenceId: "SEQ-MAIN",
          actionId: "FIELD-SURVEY",
          questionResults: [{ questionId: "FIELD-SURVEY:1", questionText: "Saha güvenli mi?", selectedOptionIndex: 0, isCorrect: true }],
          photoResults: [{ slotLabel: "Pano", wasCaptured: true, isAligned: true, alignmentScore: 0.94 }],
          completionTime: 42,
        }));
      }
      const questions = 5 + Math.floor(random() * 5);
      let correct = 0,
        mistakes = 0;
      for (let q = 0; q < questions; q++) {
        const isCorrect = random() > 0.14 + employeeIndex * 0.018;
        if (isCorrect) correct++;
        else mistakes++;
        events.push(
          envelope("QuizAnswered", 4 + q * 3, {
            actionId: `Q-${q + 1}`,
            sequenceId: `SEQ-${(q % 3) + 1}`,
            questionId: `Q-${q + 1}`,
            isCorrect,
            attempts: isCorrect ? 1 : 2,
            timeSpent: 12 + Math.floor(random() * 38),
          }),
        );
        events.push(
          envelope("ActionCompleted", 5 + q * 3, {
            actionId: `Q-${q + 1}`,
            sequenceId: `SEQ-${(q % 3) + 1}`,
            type: "quiz",
            result: "success",
          }),
        );
        if (!isCorrect)
          events.push(
            envelope("MistakeRecorded", 4 + q * 3, {
              actionId: `Q-${q + 1}`,
              sequenceId: `SEQ-${(q % 3) + 1}`,
              mistakeType: "wrong_answer",
              severity: 1,
            }),
          );
      }
      const interrupted = employee.id === "TEST001" && run === runCount - 1;
      if (interrupted) {
        events.push(envelope("SessionEnded", 34, { reason: "application_quit", timeSpent: 2040 }));
        continue;
      }
      events.push(envelope("QuizSummary", 34, {
        totalQuestions: questions,
        correctAnswers: correct,
        wrongAnswers: mistakes,
        accuracy: correct / questions,
      }));
      events.push(envelope("SequenceCompleted", 35, {
        sequenceId: "SEQ-MAIN",
        completed: true,
        timeSpent: 2100,
        mistakes,
      }));
      events.push(
        envelope("LevelCompleted", 38, {
          completed: true,
          score: Math.round((correct / questions) * 100),
          mistakes,
          timeSpent: 2280,
        }),
      );
    }
  });
  events.sort((a, b) => a.clientTimestamp.localeCompare(b.clientTimestamp));
  return {
    IS_MOCK: true,
    TODAY: today,
    content: { levels },
    quizBank: {},
    employees,
    managers: [
      {
        id: "SUPER_ADMIN",
        name: "CollbrAI Süper Admin",
        role: "super_admin",
        tenantId: "platform",
      },
      {
        id: "ADMIN_DEMO",
        name: "CEDAŞ Kurum Yöneticisi",
        role: "admin",
        tenantId: "tenant-cedas",
      },
    ],
    events,
  };
}
