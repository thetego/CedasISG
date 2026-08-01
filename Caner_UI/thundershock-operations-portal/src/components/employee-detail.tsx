import { useMemo, useState } from "react";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Download,
  ListTree,
  ShieldAlert,
  Target,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { DialogTitle } from "@/components/ui/dialog";
import { buildEmployeeDetail, eventTitle, type SessionStatus } from "@/lib/telemetry-detail";
import { cn } from "@/lib/utils";
import type { Bootstrap, Employee, EventRecord } from "@/types";

type DetailTab = "sessions" | "timeline" | "scenarios" | "mistakes" | "coverage";
type StatItem = [label: string, value: string | number, icon: LucideIcon, color: string];

const statusLabels: Record<SessionStatus, string> = {
  completed: "Tamamlandı",
  interrupted: "Kesildi",
  active: "Devam ediyor",
  incomplete: "Eksik kapanış",
};

function statusTone(status: SessionStatus) {
  return status === "completed" ? "green" : status === "active" ? "blue" : status === "interrupted" ? "red" : "amber";
}

function duration(seconds: number | undefined) {
  if (seconds === undefined || !Number.isFinite(seconds)) return "—";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = Math.round(seconds % 60);
  return hours ? `${hours} sa ${minutes} dk` : minutes ? `${minutes} dk ${rest} sn` : `${rest} sn`;
}

function dateTime(value: string | undefined) {
  return value ? new Date(value).toLocaleString("tr-TR") : "—";
}

function severityTone(value: number) {
  return value === 3 ? "red" : value === 2 ? "amber" : "slate";
}

function payloadHighlights(event: EventRecord) {
  const payload = event.payload;
  return [
    payload.sequenceId && `Sekans: ${payload.sequenceId}`,
    payload.actionId && `Aksiyon: ${payload.actionId}`,
    payload.type && `Tür: ${payload.type}`,
    payload.result && `Sonuç: ${payload.result}`,
    payload.questionId && `Soru: ${payload.questionId}`,
    payload.isCorrect !== undefined && `Doğru: ${payload.isCorrect ? "Evet" : "Hayır"}`,
    payload.attempts !== undefined && `Deneme: ${payload.attempts}`,
    payload.severity !== undefined && `Önem: ${payload.severity}`,
    payload.score !== undefined && `Puan: ${payload.score}`,
    payload.timeSpent !== undefined && `Süre: ${duration(payload.timeSpent)}`,
  ].filter(Boolean) as string[];
}

export function EmployeeDetailPanel({
  data,
  employee,
  onExport,
}: {
  data: Bootstrap;
  employee: Employee;
  onExport: () => void;
}) {
  const detail = useMemo(() => buildEmployeeDetail(data, employee), [data, employee]);
  const [tab, setTab] = useState<DetailTab>("sessions");
  const [scenarioFilter, setScenarioFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [selectedSessionId, setSelectedSessionId] = useState(detail.sessions[0]?.id || "");
  const [eventTypeFilter, setEventTypeFilter] = useState("");
  const selectedSession = detail.sessions.find((session) => session.id === selectedSessionId) || detail.sessions[0];
  const filteredSessions = detail.sessions
    .filter((session) => !scenarioFilter || session.levelId === scenarioFilter)
    .filter((session) => !statusFilter || session.status === statusFilter);
  const timeline = (selectedSession?.events || [])
    .filter((event) => !eventTypeFilter || event.eventType === eventTypeFilter)
    .slice(0, 500);

  const tabs: Array<[DetailTab, string, number]> = [
    ["sessions", "Oturumlar", detail.sessions.length],
    ["timeline", "Zaman çizelgesi", selectedSession?.events.length || 0],
    ["scenarios", "Senaryolar", detail.scenarios.filter((scenario) => scenario.sessions > 0).length],
    ["mistakes", "Hatalar", detail.mistakes.length],
    ["coverage", "Olay kapsamı", Object.keys(detail.eventTypeCounts).length],
  ];

  return (
    <div>
      <div className="border-b px-6 pb-5 pt-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <DialogTitle>{employee.name}</DialogTitle>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
              <span>{employee.id}</span><span>·</span><span>{employee.department || "Departman yok"}</span><span>·</span><span>{employee.location || "Konum yok"}</span>
            </div>
          </div>
          <Button variant="outline" onClick={onExport}><Download size={16} /> Tüm detayları CSV indir</Button>
        </div>
        <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
          {([
            ["Oturum", detail.totals.sessions, Clock3, "text-blue-600 bg-blue-50"],
            ["Tamamlanan", detail.totals.completed, CheckCircle2, "text-emerald-600 bg-emerald-50"],
            ["Aksiyon", detail.totals.actions, Target, "text-violet-600 bg-violet-50"],
            ["Doğruluk", detail.totals.accuracy === undefined ? "—" : `%${detail.totals.accuracy.toFixed(1)}`, Activity, "text-cyan-600 bg-cyan-50"],
            ["Hata", detail.totals.mistakes, AlertTriangle, "text-amber-600 bg-amber-50"],
            ["Kritik", detail.totals.criticalMistakes, ShieldAlert, "text-red-600 bg-red-50"],
          ] satisfies StatItem[]).map(([label, value, Icon, color]) => (
            <div key={label} className="rounded-xl border bg-white p-3">
              <div className={cn("grid size-8 place-items-center rounded-lg", color)}><Icon size={16} /></div>
              <b className="mt-3 block text-xl">{value}</b>
              <span className="text-[11px] text-slate-500">{label}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="flex gap-1 overflow-x-auto border-b bg-slate-50 px-6 py-2">
        {tabs.map(([id, label, count]) => (
          <button key={id} onClick={() => setTab(id)} className={cn("whitespace-nowrap rounded-lg px-3 py-2 text-xs font-semibold transition", tab === id ? "bg-white text-blue-700 shadow-sm" : "text-slate-500 hover:text-slate-900")}>
            {label} <span className="ml-1 text-[10px] opacity-60">{count}</span>
          </button>
        ))}
      </div>

      <div className="p-6">
        {detail.events.length === 0 ? (
          <div className="rounded-2xl border border-dashed p-12 text-center">
            <ListTree className="mx-auto text-slate-300" size={34} />
            <h3 className="mt-4 font-bold">Henüz telemetri oluşmadı</h3>
            <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-slate-500">Bu çalışan için veri geldiğinde oturum, senaryo, aksiyon, quiz, sürükle-bırak, anket ve hata ayrıntıları burada otomatik görünecek.</p>
          </div>
        ) : tab === "sessions" ? (
          <div>
            <div className="mb-4 flex flex-wrap gap-3">
              <select value={scenarioFilter} onChange={(event) => setScenarioFilter(event.target.value)} className="compact-control max-w-xs">
                <option value="">Tüm senaryolar</option>
                {detail.scenarios.map((scenario) => <option key={scenario.levelId} value={scenario.levelId}>{scenario.levelName}</option>)}
              </select>
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} className="compact-control max-w-52">
                <option value="">Tüm durumlar</option>
                {Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
              <Badge>{filteredSessions.length} kayıt</Badge>
            </div>
            <div className="overflow-x-auto rounded-xl border">
              <table className="data-table min-w-[1050px]">
                <thead><tr>{["Başlangıç", "Senaryo", "Durum", "Süre", "Sekans", "Aksiyon", "Quiz", "Puan", "Hata", "Kalite", ""].map((label) => <th key={label}>{label}</th>)}</tr></thead>
                <tbody>{filteredSessions.map((session) => (
                  <tr key={session.id} className="hover:bg-slate-50">
                    <td><b className="text-xs">{dateTime(session.startedAt)}</b><div className="mt-1 max-w-36 truncate font-mono text-[10px] text-slate-400" title={session.id}>{session.id}</div></td>
                    <td><b className="text-sm">{session.levelName}</b><div className="text-[10px] text-slate-400">{session.levelId}</div></td>
                    <td><Badge tone={statusTone(session.status)}>{statusLabels[session.status]}</Badge></td>
                    <td>{duration(session.durationSeconds)}</td>
                    <td>{session.sequenceIds.length}</td><td>{session.actions}</td>
                    <td>{session.quizAnswers ? `%${session.accuracy?.toFixed(0)} · ${session.quizAnswers}` : "—"}</td>
                    <td>{session.score ?? "—"}</td>
                    <td><Badge tone={session.criticalMistakes ? "red" : session.mistakes ? "amber" : "green"}>{session.mistakes}</Badge></td>
                    <td>{session.warnings.length ? <span className="text-xs font-semibold text-amber-700" title={session.warnings.join(" · ")}>{session.warnings.length} uyarı</span> : <span className="text-xs text-emerald-600">Tam</span>}</td>
                    <td><Button size="sm" variant="ghost" onClick={() => { setSelectedSessionId(session.id); setTab("timeline"); }}>İncele</Button></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          </div>
        ) : tab === "timeline" ? (
          <div>
            <div className="mb-5 grid gap-3 lg:grid-cols-[1fr_260px]">
              <select value={selectedSession?.id || ""} onChange={(event) => setSelectedSessionId(event.target.value)} className="compact-control">
                {detail.sessions.map((session) => <option key={session.id} value={session.id}>{dateTime(session.startedAt)} · {session.levelName} · {statusLabels[session.status]}</option>)}
              </select>
              <select value={eventTypeFilter} onChange={(event) => setEventTypeFilter(event.target.value)} className="compact-control">
                <option value="">Tüm olay türleri</option>
                {(selectedSession?.eventTypes || []).map((type) => <option key={type}>{type}</option>)}
              </select>
            </div>
            {selectedSession && (
              <div className="mb-5 grid gap-3 rounded-xl border bg-slate-50 p-4 sm:grid-cols-2 lg:grid-cols-5">
                <div><span className="detail-label">Durum</span><div className="mt-1"><Badge tone={statusTone(selectedSession.status)}>{statusLabels[selectedSession.status]}</Badge></div></div>
                <div><span className="detail-label">Süre</span><b className="detail-value">{duration(selectedSession.durationSeconds)}</b></div>
                <div><span className="detail-label">Sekans</span><b className="detail-value">{selectedSession.sequenceIds.length}</b></div>
                <div><span className="detail-label">Aksiyon</span><b className="detail-value">{selectedSession.actions}</b></div>
                <div><span className="detail-label">Veri kalitesi</span><b className="detail-value">{selectedSession.warnings.length ? `${selectedSession.warnings.length} uyarı` : "Tam"}</b></div>
              </div>
            )}
            <div className="relative space-y-3 before:absolute before:bottom-4 before:left-[15px] before:top-4 before:w-px before:bg-slate-200">
              {timeline.map((event) => (
                <div key={event.eventId} className="relative flex gap-4">
                  <span className={cn("relative z-10 mt-4 size-[31px] shrink-0 rounded-full border-4 border-white", event.eventType === "MistakeRecorded" ? "bg-red-500" : event.eventType === "LevelCompleted" ? "bg-emerald-500" : "bg-blue-500")} />
                  <div className="min-w-0 flex-1 rounded-xl border bg-white p-4">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                      <div><b className="text-sm">{eventTitle(event)}</b><div className="mt-1 font-mono text-[10px] text-slate-400">{event.eventType} · {event.eventId}</div></div>
                      <span className="whitespace-nowrap text-xs text-slate-500">{dateTime(event.clientTimestamp)}</span>
                    </div>
                    {payloadHighlights(event).length > 0 && <div className="mt-3 flex flex-wrap gap-1.5">{payloadHighlights(event).map((item) => <Badge key={item} tone="slate">{item}</Badge>)}</div>}
                    <details className="mt-3"><summary className="cursor-pointer text-xs font-semibold text-blue-600">Tüm event alanlarını göster</summary><pre className="mt-2 max-h-64 overflow-auto rounded-lg bg-slate-950 p-3 text-[10px] leading-5 text-slate-200">{JSON.stringify(event, null, 2)}</pre></details>
                  </div>
                </div>
              ))}
            </div>
            {(selectedSession?.events.length || 0) > 500 && <p className="mt-4 text-xs text-amber-700">Performans için ilk 500 olay gösteriliyor. CSV dışa aktarımı tüm kayıtları içerir.</p>}
          </div>
        ) : tab === "scenarios" ? (
          <div className="overflow-x-auto rounded-xl border">
            <table className="data-table min-w-[900px]"><thead><tr>{["Senaryo", "Oturum", "Tamamlanan", "Kesilen/Eksik", "Sekans", "Aksiyon", "Doğruluk", "Hata", "Kritik", "Ort. süre"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{detail.scenarios.map((scenario) => <tr key={scenario.levelId}><td><b>{scenario.levelName}</b><div className="text-[10px] text-slate-400">{scenario.levelId}</div></td><td>{scenario.sessions}</td><td>{scenario.completed}</td><td>{scenario.interrupted + scenario.incomplete}</td><td>{scenario.sequenceIds.length}</td><td>{scenario.actions}</td><td>{scenario.accuracy === undefined ? "—" : `%${scenario.accuracy.toFixed(1)}`}</td><td>{scenario.mistakes}</td><td>{scenario.criticalMistakes}</td><td>{duration(scenario.averageDurationSeconds)}</td></tr>)}</tbody>
            </table>
          </div>
        ) : tab === "mistakes" ? (
          <div className="space-y-3">
            {detail.mistakes.length === 0 ? <div className="rounded-xl border border-dashed p-10 text-center text-sm text-slate-500">Bu çalışan için hata kaydı bulunmuyor.</div> : detail.mistakes.map((mistake) => (
              <Card key={mistake.event.eventId}><CardContent className="p-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                  <div className="flex gap-3"><div className="grid size-9 shrink-0 place-items-center rounded-lg bg-red-50 text-red-600"><AlertTriangle size={17} /></div><div><div className="flex flex-wrap items-center gap-2"><b className="text-sm">{mistake.mistakeType}</b><Badge tone={severityTone(mistake.severity)}>Önem {mistake.severity}</Badge></div><p className="mt-1 text-xs text-slate-500">{mistake.levelName} · {mistake.sequenceId} · {mistake.actionId}</p><p className="mt-1 font-mono text-[10px] text-slate-400">{mistake.actionKey}</p></div></div>
                  <span className="text-xs text-slate-500">{dateTime(mistake.event.clientTimestamp)}</span>
                </div>
                {(mistake.selectedAnswer || mistake.correctAnswer) && <div className="mt-4 grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-2"><div><span className="detail-label">Verilen cevap</span><b className="detail-value text-red-700">{mistake.selectedAnswer || "Kaydedilmedi"}</b></div><div><span className="detail-label">Doğru cevap</span><b className="detail-value text-emerald-700">{mistake.correctAnswer || "Kaydedilmedi"}</b></div></div>}
              </CardContent></Card>
            ))}
          </div>
        ) : (
          <div>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-6">
              {Object.entries(detail.eventTypeCounts).sort(([left], [right]) => left.localeCompare(right)).map(([type, count]) => <div key={type} className="rounded-xl border p-4"><span className="block truncate text-[11px] font-semibold text-slate-500" title={type}>{type}</span><b className="mt-1 block text-xl">{count}</b></div>)}
            </div>
            <div className="mt-5 rounded-xl border bg-slate-50 p-4 text-sm text-slate-600"><b>Toplam izlenebilir süre:</b> {duration(detail.totals.durationSeconds)} · <b>Toplam event:</b> {detail.events.length} · <b>Şema kapsamı:</b> {Object.keys(detail.eventTypeCounts).length}/11 olay türü</div>
          </div>
        )}
      </div>
    </div>
  );
}
