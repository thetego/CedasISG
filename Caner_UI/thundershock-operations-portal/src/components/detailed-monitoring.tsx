import { useMemo, useState } from "react";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Download,
  Filter,
  ListTree,
  Search,
  ShieldAlert,
  Target,
  Users,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { buildTrainingSessions, eventTitle, humanizeTelemetryValue, type SessionStatus } from "@/lib/telemetry-detail";
import type { Bootstrap, EventRecord, EventType } from "@/types";

type MonitoringTab = "sessions" | "mistakes" | "events" | "quality";
type StatItem = [label: string, value: string | number, icon: LucideIcon, color: string];

const eventTypeLabels: Record<EventType, string> = {
  LevelStarted: "Senaryo başladı",
  LevelCompleted: "Senaryo tamamlandı",
  SequenceStarted: "Sekans başladı",
  SequenceCompleted: "Sekans tamamlandı",
  ActionCompleted: "Aksiyon tamamlandı",
  QuizAnswered: "Soru yanıtlandı",
  QuizSummary: "Soru özeti",
  DragDropAttempt: "Sürükle-bırak denemesi",
  MistakeRecorded: "Hata kaydedildi",
  SurveyCompleted: "Saha anketi tamamlandı",
  SessionEnded: "Oturum sonlandırıldı",
};

const statusLabels: Record<SessionStatus, string> = {
  completed: "Tamamlandı",
  interrupted: "Kesildi",
  active: "Devam ediyor",
  incomplete: "Eksik kapanış",
};

function statusTone(status: SessionStatus) {
  return status === "completed" ? "green" : status === "active" ? "blue" : status === "interrupted" ? "red" : "amber";
}

function dateTime(value: string | undefined) {
  return value ? new Date(value).toLocaleString("tr-TR") : "—";
}

function duration(seconds: number | undefined) {
  if (seconds === undefined || !Number.isFinite(seconds)) return "—";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = Math.round(seconds % 60);
  return hours ? `${hours} sa ${minutes} dk` : minutes ? `${minutes} dk ${rest} sn` : `${rest} sn`;
}

function eventResult(event: EventRecord) {
  if (event.eventType === "QuizAnswered")
    return `${event.payload.isCorrect ? "Doğru" : "Yanlış"}${event.payload.selectedAnswer ? ` · ${event.payload.selectedAnswer}` : ""}`;
  if (event.eventType === "MistakeRecorded")
    return `${humanizeTelemetryValue(event.payload.mistakeType)} · önem ${event.payload.severity || 1}`;
  if (event.payload.result) return humanizeTelemetryValue(event.payload.result);
  if (event.payload.score !== undefined) return `Puan ${event.payload.score}`;
  if (event.payload.completed !== undefined) return event.payload.completed ? "Tamamlandı" : "Tamamlanmadı";
  return "—";
}

export function DetailedMonitoring({
  data,
  onExport,
}: {
  data: Bootstrap;
  onExport: (events: EventRecord[]) => void;
}) {
  const sessions = useMemo(() => buildTrainingSessions(data, data.events), [data]);
  const [tab, setTab] = useState<MonitoringTab>("sessions");
  const [employeeId, setEmployeeId] = useState("");
  const [levelId, setLevelId] = useState("");
  const [status, setStatus] = useState("");
  const [eventType, setEventType] = useState("");
  const [query, setQuery] = useState("");
  const employees = new Map(data.employees.map((employee) => [employee.id, employee]));
  const levels = new Map(data.content.levels.map((level) => [level.emittedLevelId, level.name]));
  const normalizedQuery = query.trim().toLocaleLowerCase("tr-TR");
  const matchesQuery = (values: Array<string | undefined>) =>
    !normalizedQuery || values.some((value) => String(value || "").toLocaleLowerCase("tr-TR").includes(normalizedQuery));
  const filteredSessions = sessions.filter((session) =>
    (!employeeId || session.employeeId === employeeId) &&
    (!levelId || session.levelId === levelId) &&
    (!status || session.status === status) &&
    (!eventType || session.eventTypes.includes(eventType)) &&
    matchesQuery([session.id, session.employeeId, employees.get(session.employeeId)?.name, session.levelName, ...session.sequenceIds]),
  );
  const filteredEvents = [...data.events]
    .filter((event) =>
      (!employeeId || event.employeeId === employeeId) &&
      (!levelId || event.payload.levelId === levelId) &&
      (!eventType || event.eventType === eventType) &&
      (!status || filteredSessions.some((session) => session.id === event.payload.sessionId)) &&
      matchesQuery([
        event.eventId,
        event.employeeId,
        employees.get(event.employeeId)?.name,
        event.payload.sessionId,
        event.payload.levelId,
        event.payload.sequenceId,
        event.payload.actionId,
        event.payload.actionKey,
        event.payload.mistakeType,
      ]),
    )
    .sort((left, right) => right.clientTimestamp.localeCompare(left.clientTimestamp));
  const mistakes = filteredEvents.filter((event) => event.eventType === "MistakeRecorded");
  const critical = mistakes.filter((event) => Number(event.payload.severity || 1) === 3).length;
  const incomplete = filteredSessions.filter((session) => session.status !== "completed").length;
  const warnings = filteredSessions.reduce((total, session) => total + session.warnings.length, 0);
  const missingSession = data.events.filter((event) => !event.payload.sessionId).length;
  const missingLevel = data.events.filter((event) => !event.payload.levelId).length;
  const missingActionKey = data.events.filter((event) => event.eventType === "ActionCompleted" && !event.payload.actionKey).length;
  const eventTypeCounts = new Map<string, number>();
  filteredEvents.forEach((event) => eventTypeCounts.set(event.eventType, (eventTypeCounts.get(event.eventType) || 0) + 1));
  const tabs: Array<[MonitoringTab, string, number]> = [
    ["sessions", "Tüm oturumlar", filteredSessions.length],
    ["mistakes", "Tüm hatalar", mistakes.length],
    ["events", "Olay akışı", filteredEvents.length],
    ["quality", "Veri kalitesi", warnings + missingSession + missingLevel + missingActionKey],
  ];

  function resetFilters() {
    setEmployeeId("");
    setLevelId("");
    setStatus("");
    setEventType("");
    setQuery("");
  }

  return (
    <div className="fade-up">
      <div className="mb-6 flex flex-col justify-between gap-4 xl:flex-row xl:items-end">
        <div>
          <div className="flex items-center gap-2"><Badge tone="blue"><Activity className="mr-1" size={12} /> Merkezi görünüm</Badge></div>
          <h1 className="mt-3 text-2xl font-bold tracking-[-.03em] text-slate-950">Detaylı İzleme</h1>
          <p className="mt-1 text-sm text-slate-500">Tüm çalışan, oturum, senaryo, sekans, aksiyon ve hata kayıtlarını tek ekranda inceleyin.</p>
        </div>
        <Button onClick={() => onExport(filteredEvents)}><Download size={16} /> Filtrelenmiş veriyi indir</Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        {([
          ["Çalışan", new Set(filteredEvents.map((event) => event.employeeId)).size, Users, "bg-blue-50 text-blue-600"],
          ["Oturum", filteredSessions.length, Clock3, "bg-violet-50 text-violet-600"],
          ["Aksiyon", filteredEvents.filter((event) => event.eventType === "ActionCompleted").length, Target, "bg-cyan-50 text-cyan-600"],
          ["Tamamlanan", filteredSessions.filter((session) => session.status === "completed").length, CheckCircle2, "bg-emerald-50 text-emerald-600"],
          ["Hata", mistakes.length, AlertTriangle, "bg-amber-50 text-amber-600"],
          ["Kritik / açık", `${critical} / ${incomplete}`, ShieldAlert, "bg-red-50 text-red-600"],
        ] satisfies StatItem[]).map(([label, value, Icon, color]) => (
          <Card key={label}><CardContent className="p-4"><div className={`grid size-9 place-items-center rounded-xl ${color}`}><Icon size={17} /></div><b className="mt-3 block text-xl">{value}</b><span className="text-xs text-slate-500">{label}</span></CardContent></Card>
        ))}
      </div>

      <Card className="mt-4">
        <CardContent className="p-4">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-[1.3fr_1fr_1fr_1fr_1fr_auto]">
            <label className="relative"><Search className="absolute left-3 top-2.5 text-slate-400" size={16} /><input value={query} onChange={(event) => setQuery(event.target.value)} className="compact-control pl-9" placeholder="Oturum, çalışan, aksiyon veya hata ara" /></label>
            <select value={employeeId} onChange={(event) => setEmployeeId(event.target.value)} className="compact-control"><option value="">Tüm çalışanlar</option>{data.employees.map((employee) => <option key={employee.id} value={employee.id}>{employee.name} · {employee.id}</option>)}</select>
            <select value={levelId} onChange={(event) => setLevelId(event.target.value)} className="compact-control"><option value="">Tüm senaryolar</option>{data.content.levels.map((level) => <option key={level.emittedLevelId} value={level.emittedLevelId}>{level.name}</option>)}</select>
            <select value={status} onChange={(event) => setStatus(event.target.value)} className="compact-control"><option value="">Tüm oturum durumları</option>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
            <select value={eventType} onChange={(event) => setEventType(event.target.value)} className="compact-control"><option value="">Tüm olay türleri</option>{[...new Set(data.events.map((event) => event.eventType))].sort().map((type) => <option key={type} value={type}>{eventTypeLabels[type]}</option>)}</select>
            <Button variant="ghost" onClick={resetFilters}><Filter size={15} /> Temizle</Button>
          </div>
        </CardContent>
      </Card>

      <div className="mt-4 flex gap-1 overflow-x-auto rounded-t-xl border border-b-0 bg-slate-50 px-3 py-2">
        {tabs.map(([id, label, count]) => <button key={id} onClick={() => setTab(id)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-semibold transition ${tab === id ? "bg-white text-blue-700 shadow-sm" : "text-slate-500 hover:text-slate-900"}`}>{label} <span className="ml-1 opacity-60">{count}</span></button>)}
      </div>

      <Card className="rounded-t-none">
        <CardContent className="overflow-x-auto p-0">
          {tab === "sessions" ? (
            <table className="data-table min-w-[1380px]"><thead><tr>{["Başlangıç", "Çalışan", "Senaryo", "Durum", "Süre", "Sekanslar", "Aksiyon", "Soru", "Puan", "Hata", "Kritik", "Bitiş", "Veri kalitesi"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{filteredSessions.map((session) => { const employee = employees.get(session.employeeId); return <tr key={session.id}><td><b className="text-xs">{dateTime(session.startedAt)}</b><div className="mt-1 max-w-36 truncate font-mono text-[10px] text-slate-400" title={session.id}>{session.id}</div></td><td><b>{employee?.name || session.employeeId}</b><div className="text-[10px] text-slate-400">{session.employeeId} · {employee?.department || "—"}</div></td><td><b>{session.levelName}</b><div className="text-[10px] text-slate-400">{session.levelId}</div></td><td><Badge tone={statusTone(session.status)}>{statusLabels[session.status]}</Badge></td><td>{duration(session.durationSeconds)}</td><td><span title={session.sequenceIds.join(" · ")}>{session.sequenceIds.length} sekans</span></td><td>{session.actions}</td><td>{session.quizAnswers ? `%${session.accuracy?.toFixed(1)} · ${session.quizAnswers}` : "—"}</td><td>{session.score ?? "—"}</td><td>{session.mistakes}</td><td>{session.criticalMistakes}</td><td>{dateTime(session.endedAt)}</td><td>{session.warnings.length ? <Badge tone="amber">{session.warnings.length} uyarı</Badge> : <Badge tone="green">Tam</Badge>}</td></tr>; })}</tbody>
            </table>
          ) : tab === "mistakes" ? (
            <table className="data-table min-w-[1200px]"><thead><tr>{["Zaman", "Çalışan", "Senaryo", "Sekans", "Aksiyon", "Aksiyon anahtarı", "Hata türü", "Önem", "Oturum"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{mistakes.map((event) => <tr key={event.eventId}><td>{dateTime(event.clientTimestamp)}</td><td><b>{employees.get(event.employeeId)?.name || event.employeeId}</b><div className="text-[10px] text-slate-400">{event.employeeId}</div></td><td>{levels.get(event.payload.levelId || "") || event.payload.levelId || "—"}</td><td>{event.payload.sequenceId || "—"}</td><td>{event.payload.actionId || "—"}</td><td className="max-w-60 truncate font-mono text-[10px]" title={event.payload.actionKey}>{event.payload.actionKey || "—"}</td><td>{humanizeTelemetryValue(event.payload.mistakeType)}</td><td><Badge tone={event.payload.severity === 3 ? "red" : event.payload.severity === 2 ? "amber" : "slate"}>{event.payload.severity || 1}</Badge></td><td className="font-mono text-[10px]">{event.payload.sessionId}</td></tr>)}</tbody>
            </table>
          ) : tab === "events" ? (
            <table className="data-table min-w-[1450px]"><thead><tr>{["Zaman", "Çalışan", "Olay", "Sonuç", "Senaryo", "Sekans", "Aksiyon", "Soru", "Deneme", "Süre", "Oturum", "Olay kimliği"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{filteredEvents.slice(0, 1000).map((event) => <tr key={event.eventId}><td>{dateTime(event.clientTimestamp)}</td><td><b>{employees.get(event.employeeId)?.name || event.employeeId}</b><div className="text-[10px] text-slate-400">{event.employeeId}</div></td><td><b>{eventTitle(event)}</b><div className="text-[10px] text-slate-400">{event.eventType}</div></td><td>{eventResult(event)}</td><td>{levels.get(event.payload.levelId || "") || event.payload.levelId || "—"}</td><td>{event.payload.sequenceId || "—"}</td><td>{event.payload.actionId || "—"}</td><td>{event.payload.questionId || "—"}</td><td>{event.payload.attempts ?? "—"}</td><td>{duration(event.payload.timeSpent ?? event.payload.duration)}</td><td className="font-mono text-[10px]">{event.payload.sessionId}</td><td className="max-w-40 truncate font-mono text-[10px]" title={event.eventId}>{event.eventId}</td></tr>)}</tbody>
            </table>
          ) : (
            <div className="grid gap-4 p-5 xl:grid-cols-[1fr_1.4fr]">
              <div className="space-y-2">{[
                ["Oturum kalite uyarısı", warnings],
                ["Oturum kimliği eksik olay", missingSession],
                ["Senaryo kimliği eksik olay", missingLevel],
                ["Eski aksiyon anahtarı eksikliği", missingActionKey],
                ["Veri alımında reddedilen", data.quality?.rejected || 0],
                ["Veri alımında tekrarlı", data.quality?.duplicate || 0],
              ].map(([label, value]) => <div key={label} className="flex items-center justify-between rounded-xl border p-3"><span className="text-sm font-semibold text-slate-600">{label}</span><Badge tone={Number(value) ? "amber" : "green"}>{value}</Badge></div>)}</div>
              <div><CardHeader className="px-0 pt-0"><CardTitle>Filtrelenmiş olay kapsamı</CardTitle><Badge tone="blue">{eventTypeCounts.size}/11</Badge></CardHeader><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{[...eventTypeCounts.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([type, count]) => <div key={type} className="rounded-xl border p-3"><span className="block truncate text-[11px] font-semibold text-slate-600">{eventTypeLabels[type as EventType] || "Bilinmeyen olay"}</span><span className="block truncate font-mono text-[9px] text-slate-400">{type}</span><b className="mt-1 block text-xl">{count}</b></div>)}</div>{(data.quality?.invalidSamples || []).map((sample, index) => <details key={`${sample.eventType}-${index}`} className="mt-2 rounded-xl border border-amber-200 bg-amber-50 p-3"><summary className="cursor-pointer text-xs font-semibold text-amber-800">{eventTypeLabels[sample.eventType as EventType] || "Bilinmeyen olay"} şema hatası</summary><p className="mt-2 text-xs text-amber-700">{sample.errors.join(" · ")}</p></details>)}</div>
            </div>
          )}
          {((tab === "sessions" && filteredSessions.length === 0) || (tab === "mistakes" && mistakes.length === 0) || (tab === "events" && filteredEvents.length === 0)) && <div className="p-12 text-center"><ListTree className="mx-auto text-slate-300" size={32} /><p className="mt-3 text-sm text-slate-500">Seçili filtrelerle eşleşen kayıt bulunamadı.</p></div>}
          {tab === "events" && filteredEvents.length > 1000 && <p className="border-t bg-amber-50 p-3 text-xs text-amber-700">Ekranda son 1.000 olay gösteriliyor. CSV dışa aktarımı filtrelenmiş kayıtların tamamını içerir.</p>}
        </CardContent>
      </Card>
    </div>
  );
}
