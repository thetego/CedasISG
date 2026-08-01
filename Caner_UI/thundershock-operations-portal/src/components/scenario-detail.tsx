import { useMemo, useState } from "react";
import { Activity, AlertTriangle, CheckCircle2, Clock3, Download, ListTree, ShieldAlert, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { DialogTitle } from "@/components/ui/dialog";
import { buildScenarioDetail, eventTitle, humanizeTelemetryValue } from "@/lib/telemetry-detail";
import type { Bootstrap, Level } from "@/types";

type ScenarioTab = "employees" | "sequences" | "mistakes" | "events";

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

export function ScenarioDetailPanel({
  data,
  level,
  onExport,
}: {
  data: Bootstrap;
  level: Level;
  onExport: () => void;
}) {
  const detail = useMemo(() => buildScenarioDetail(data, level.emittedLevelId), [data, level.emittedLevelId]);
  const [tab, setTab] = useState<ScenarioTab>("employees");
  const summary = detail.summary;
  const tabs: Array<[ScenarioTab, string, number]> = [
    ["employees", "Çalışanlar", detail.employees.length],
    ["sequences", "Sekanslar", detail.sequences.length],
    ["mistakes", "Hatalar", detail.mistakes.length],
    ["events", "Olay akışı", detail.events.length],
  ];

  return (
    <div>
      <div className="border-b px-6 pb-5 pt-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <DialogTitle>{level.name}</DialogTitle>
            <p className="mt-1 font-mono text-xs text-slate-400">{level.emittedLevelId}</p>
          </div>
          <Button variant="outline" onClick={onExport}><Download size={16} /> Tüm olayları CSV indir</Button>
        </div>
        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-8">
          {[
            ["Oturum", summary?.sessions || 0],
            ["Tamamlanan", summary?.completed || 0],
            ["Çalışan", summary?.employees || 0],
            ["Sekans", summary?.sequenceIds.length || 0],
            ["Aksiyon", summary?.actions || 0],
            ["Doğruluk", summary?.accuracy === undefined ? "—" : `%${summary.accuracy.toFixed(1)}`],
            ["Hata", summary?.mistakes || 0],
            ["Kritik", summary?.criticalMistakes || 0],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl border bg-white p-3">
              <b className="block text-xl">{value}</b>
              <span className="mt-1 block text-[11px] text-slate-500">{label}</span>
            </div>
          ))}
        </div>
        {summary && (
          <div className="mt-3 flex flex-wrap gap-2">
            <Badge tone="blue"><Clock3 className="mr-1" size={12} /> Ort. tamamlanma {duration(summary.averageDurationSeconds)}</Badge>
            <Badge tone={summary.incomplete + summary.interrupted ? "amber" : "green"}>{summary.interrupted} kesilen · {summary.incomplete} eksik/aktif</Badge>
          </div>
        )}
      </div>

      <div className="flex gap-1 overflow-x-auto border-b bg-slate-50 px-6 py-2">
        {tabs.map(([id, label, count]) => (
          <button key={id} onClick={() => setTab(id)} className={`whitespace-nowrap rounded-lg px-3 py-2 text-xs font-semibold transition ${tab === id ? "bg-white text-blue-700 shadow-sm" : "text-slate-500 hover:text-slate-900"}`}>
            {label} <span className="ml-1 text-[10px] opacity-60">{count}</span>
          </button>
        ))}
      </div>

      <div className="p-6">
        {detail.events.length === 0 ? (
          <div className="rounded-2xl border border-dashed p-12 text-center">
            <ListTree className="mx-auto text-slate-300" size={34} />
            <h3 className="mt-4 font-bold">Bu senaryo için telemetri yok</h3>
            <p className="mt-2 text-sm text-slate-500">İlk oturum geldiğinde çalışan, sekans, aksiyon ve hata kırılımları otomatik oluşacak.</p>
          </div>
        ) : tab === "employees" ? (
          <div className="overflow-x-auto rounded-xl border">
            <table className="data-table min-w-[850px]">
              <thead><tr>{["Çalışan", "Departman", "Oturum", "Tamamlanan", "Aksiyon", "Doğruluk", "Hata", "Son aktivite"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{detail.employees.map((item) => <tr key={item.employee.id}>
                <td><b>{item.employee.name}</b><div className="text-[10px] text-slate-400">{item.employee.id}</div></td>
                <td>{item.employee.department || "—"}</td><td>{item.sessions}</td><td>{item.completed}</td><td>{item.actions}</td>
                <td>{item.accuracy === undefined ? "—" : `%${item.accuracy.toFixed(1)}`}</td>
                <td><Badge tone={item.mistakes ? "amber" : "green"}>{item.mistakes}</Badge></td><td>{dateTime(item.lastAt)}</td>
              </tr>)}</tbody>
            </table>
          </div>
        ) : tab === "sequences" ? (
          <div className="overflow-x-auto rounded-xl border">
            <table className="data-table min-w-[720px]">
              <thead><tr>{["Sekans", "Çalışan", "Toplam olay", "Aksiyon", "Soru", "Hata", "Risk"].map((label) => <th key={label}>{label}</th>)}</tr></thead>
              <tbody>{detail.sequences.map((sequence) => <tr key={sequence.sequenceId}>
                <td><b className="font-mono text-xs">{sequence.sequenceId}</b></td><td>{sequence.employees}</td><td>{sequence.events}</td><td>{sequence.actions}</td><td>{sequence.quizAnswers}</td><td>{sequence.mistakes}</td>
                <td><Badge tone={sequence.mistakes >= 3 ? "red" : sequence.mistakes ? "amber" : "green"}>{sequence.mistakes ? "İncele" : "Temiz"}</Badge></td>
              </tr>)}</tbody>
            </table>
          </div>
        ) : tab === "mistakes" ? (
          <div className="space-y-3">
            {detail.mistakes.length === 0 ? <div className="rounded-xl border border-dashed p-10 text-center text-sm text-slate-500">Bu senaryoda hata kaydı bulunmuyor.</div> : detail.mistakes.map((mistake) => {
              const employee = data.employees.find((item) => item.id === mistake.event.employeeId);
              return <Card key={mistake.event.eventId}><CardContent className="p-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                  <div className="flex gap-3"><div className="grid size-9 shrink-0 place-items-center rounded-lg bg-red-50 text-red-600"><ShieldAlert size={17} /></div><div>
                    <div className="flex flex-wrap items-center gap-2"><b className="text-sm">{humanizeTelemetryValue(mistake.mistakeType)}</b><Badge tone={mistake.severity === 3 ? "red" : mistake.severity === 2 ? "amber" : "slate"}>Önem {mistake.severity}</Badge></div>
                    <p className="mt-1 text-xs text-slate-500">{employee?.name || mistake.event.employeeId} · {mistake.sequenceId} · {mistake.actionId}</p>
                    <p className="mt-1 font-mono text-[10px] text-slate-400">{mistake.actionKey} · {mistake.sessionId}</p>
                  </div></div><span className="text-xs text-slate-500">{dateTime(mistake.event.clientTimestamp)}</span>
                </div>
                {(mistake.selectedAnswer || mistake.correctAnswer) && <div className="mt-4 grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-2"><div><span className="detail-label">Verilen cevap</span><b className="detail-value text-red-700">{mistake.selectedAnswer || "Kaydedilmedi"}</b></div><div><span className="detail-label">Doğru cevap</span><b className="detail-value text-emerald-700">{mistake.correctAnswer || "Kaydedilmedi"}</b></div></div>}
              </CardContent></Card>;
            })}
          </div>
        ) : (
          <div className="space-y-3">
            {detail.events.slice(0, 500).map((event) => {
              const employee = data.employees.find((item) => item.id === event.employeeId);
              return <div key={event.eventId} className="rounded-xl border p-4">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between"><div className="flex items-start gap-3">
                  <div className={`grid size-8 shrink-0 place-items-center rounded-lg ${event.eventType === "MistakeRecorded" ? "bg-red-50 text-red-600" : event.eventType === "LevelCompleted" ? "bg-emerald-50 text-emerald-600" : "bg-blue-50 text-blue-600"}`}>
                    {event.eventType === "MistakeRecorded" ? <AlertTriangle size={15} /> : event.eventType === "LevelCompleted" ? <CheckCircle2 size={15} /> : event.eventType === "LevelStarted" ? <Users size={15} /> : <Activity size={15} />}
                  </div><div><b className="text-sm">{eventTitle(event)}</b><p className="mt-1 text-xs text-slate-500">{employee?.name || event.employeeId} · {event.payload.sequenceId || "Sekans yok"} · {event.payload.actionId || "Aksiyon yok"}</p></div>
                </div><span className="text-xs text-slate-500">{dateTime(event.clientTimestamp)}</span></div>
                <details className="mt-3"><summary className="cursor-pointer text-xs font-semibold text-blue-600">Ham olayı göster</summary><pre className="mt-2 max-h-64 overflow-auto rounded-lg bg-slate-950 p-3 text-[10px] leading-5 text-slate-200">{JSON.stringify(event, null, 2)}</pre></details>
              </div>;
            })}
            {detail.events.length > 500 && <p className="text-xs text-amber-700">Performans için son 500 olay gösteriliyor; CSV tüm kayıtları içerir.</p>}
          </div>
        )}
      </div>
    </div>
  );
}
