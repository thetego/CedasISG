import { useEffect, useMemo, useState } from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ComposedChart,
  ResponsiveContainer,
  Legend,
  Line,
  ReferenceLine,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  Activity,
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  BarChart3,
  Bell,
  BookOpen,
  CheckCircle2,
  ChevronDown,
  ClipboardCheck,
  Clock3,
  Download,
  FileBarChart,
  Filter,
  GraduationCap,
  LayoutDashboard,
  KeyRound,
  LogOut,
  Menu,
  MoreHorizontal,
  Search,
  ScrollText,
  Plug,
  Save,
  Settings,
  ShieldCheck,
  Sparkles,
  Target,
  TrendingUp,
  Users,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";
import type { Bootstrap, Employee, SessionUser } from "@/types";

type Page =
  | "dashboard"
  | "employees"
  | "scenarios"
  | "risks"
  | "reports"
  | "access"
  | "audit"
  | "privacy"
  | "integrations"
  | "settings";
const nav = [
  ["dashboard", "Genel Bakış", LayoutDashboard, "analytics:read"],
  ["employees", "Çalışanlar", Users, "employees:read"],
  ["scenarios", "Senaryolar", BookOpen, "analytics:read"],
  ["risks", "Risk Analizi", ShieldCheck, "analytics:read"],
  ["reports", "Rapor Merkezi", FileBarChart, "reports:export"],
  ["access", "Erişim Yönetimi", KeyRound, "users:manage"],
  ["audit", "Audit Log", ScrollText, "audit:read"],
  ["privacy", "Veri Hakları", ClipboardCheck, "privacy:request"],
  ["integrations", "Entegrasyonlar", Plug, "integrations:manage"],
  ["settings", "Ayarlar", Settings, "settings:write"],
] as const;
const fmt = new Intl.NumberFormat("tr-TR");
function hasPermission(user: SessionUser, required: string) {
  return user.permissions.some(
    (permission) =>
      permission === required || permission.startsWith(required + ":"),
  );
}
function notify(
  message: string,
  tone: "success" | "info" | "error" = "success",
) {
  window.dispatchEvent(
    new CustomEvent("cedas:toast", { detail: { message, tone } }),
  );
}
function navigateTo(page: Page) {
  window.dispatchEvent(new CustomEvent("cedas:navigate", { detail: page }));
}
function downloadCsv(name: string, rows: (string | number)[][]) {
  const safe = (value: string | number) => {
    const text = String(value ?? "");
    const protectedText = /^[=+\-@]/.test(text) ? "'" + text : text;
    return /[;"\n]/.test(protectedText)
      ? `"${protectedText.replace(/"/g, '""')}"`
      : protectedText;
  };
  const csv = "\uFEFF" + rows.map((row) => row.map(safe).join(";")).join("\n"),
    url = URL.createObjectURL(
      new Blob([csv], { type: "text/csv;charset=utf-8" }),
    ),
    anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = name;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  notify(`${name} indirildi`);
}
function ToastHost() {
  const [toasts, setToasts] = useState<
    { id: number; message: string; tone: string }[]
  >([]);
  useEffect(() => {
    const handler = (event: Event) => {
      const detail = (event as CustomEvent).detail,
        id = Date.now();
      setToasts((current) => [...current, { id, ...detail }]);
      setTimeout(
        () => setToasts((current) => current.filter((item) => item.id !== id)),
        3500,
      );
    };
    window.addEventListener("cedas:toast", handler);
    return () => window.removeEventListener("cedas:toast", handler);
  }, []);
  return (
    <div className="fixed bottom-5 right-5 z-[100] space-y-2">
      {toasts.map((item) => (
        <div
          key={item.id}
          role="status"
          className={cn(
            "min-w-72 rounded-xl border bg-white px-4 py-3 text-sm font-semibold shadow-xl",
            item.tone === "error"
              ? "border-red-200 text-red-700"
              : "border-emerald-200 text-emerald-700",
          )}
        >
          <CheckCircle2 className="mr-2 inline" size={16} />
          {item.message}
        </div>
      ))}
    </div>
  );
}

function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <div className="flex items-center gap-3">
      <div className="grid size-10 place-items-center rounded-xl bg-blue-600 text-white shadow-lg shadow-blue-200">
        <Activity size={21} />
      </div>
      {!compact && (
        <div>
          <div className="text-sm font-extrabold tracking-tight text-slate-950">
            CEDAŞ
          </div>
          <div className="text-[10px] font-semibold uppercase tracking-[.16em] text-slate-400">
            Eğitim Analitiği
          </div>
        </div>
      )}
    </div>
  );
}

function Login({
  onLogin,
  isDemo,
}: {
  onLogin: (id: string, password: string, remember: boolean) => Promise<void>;
  isDemo: boolean;
}) {
  const [id, setId] = useState(isDemo ? "ADMIN_DEMO" : "");
  const [pw, setPw] = useState(isDemo ? "demo123" : "");
  const [remember, setRemember] = useState(true);
  const [busy, setBusy] = useState(false);
  return (
    <div className="h-dvh overflow-hidden bg-white">
      <div className="grid h-full lg:grid-cols-[1.08fr_.92fr]">
        <section className="relative hidden overflow-hidden border-r border-slate-200 bg-slate-50 p-12 text-slate-950 lg:flex lg:flex-col lg:justify-between">
          <div className="absolute inset-0 dot-grid opacity-30" />
          <div className="absolute -right-32 -top-32 size-[32rem] rounded-full bg-blue-100/70 blur-3xl" />
          <div className="relative">
            <Logo />
            <Badge tone="blue" className="mt-10">
              {isDemo ? "Demo analitik ortamı" : "İş güvenliği · Gerçek zamanlı analitik"}
            </Badge>
          </div>
          <div className="relative max-w-xl">
            <h1 className="text-5xl font-semibold leading-[1.08] tracking-[-.04em]">
              Sahadaki gelişimi
              <br />
              <span className="text-blue-600">ölçülebilir</span> hale getirin.
            </h1>
            <p className="mt-6 max-w-lg text-base leading-7 text-slate-600">
              Eğitim performansı, operasyonel riskler ve çalışan gelişimi tek
              bir profesyonel yönetim ekranında.
            </p>
            <div className="mt-10 grid grid-cols-3 gap-3">
              {(isDemo
                ? [["11/11", "Olay kapsamı"], ["13", "Demo çalışan"], ["RBAC", "Yetki modeli"]]
                : [["11", "Olay türü"], ["RBAC", "Yetki modeli"], ["KVKK", "Veri hakları"]]
              ).map(([v, l]) => (
                <div
                  key={l}
                  className="rounded-xl border border-slate-200 bg-white p-4"
                >
                  <b className="block text-xl">{v}</b>
                  <span className="text-xs text-slate-500">{l}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="relative flex items-center gap-2 text-xs text-slate-500">
            <ShieldCheck size={15} /> Güvenli veri katmanı · Rol bazlı erişim
          </div>
        </section>
        <main className="flex h-full items-center justify-center overflow-y-auto bg-white px-6 py-8">
          <div className="w-full max-w-[420px] fade-up">
            <div className="mb-10 lg:hidden">
              <Logo />
            </div>
            <div className="mb-8">
              <Badge tone="blue">Yönetim Portalı</Badge>
              <h2 className="mt-4 text-3xl font-bold tracking-[-.03em] text-slate-950">
                Tekrar hoş geldiniz
              </h2>
              <p className="mt-2 text-sm text-slate-500">
                Kurumsal hesabınızla güvenli oturum açın.
              </p>
            </div>
            <form
              onSubmit={async (e) => {
                e.preventDefault();
                setBusy(true);
                try {
                  await onLogin(id, pw, remember);
                } finally {
                  setBusy(false);
                }
              }}
              className="space-y-5"
            >
              <label className="block">
                <span className="mb-2 block text-sm font-semibold text-slate-700">
                  Kullanıcı kimliği
                </span>
                <div className="relative">
                  <Users
                    className="absolute left-3.5 top-3.5 text-slate-400"
                    size={18}
                  />
                  <input
                    required
                    name="username"
                    autoComplete="username"
                    value={id}
                    onChange={(e) => setId(e.target.value)}
                    className="h-12 w-full rounded-xl border border-slate-200 bg-white pl-11 pr-4 text-sm outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-50"
                  />
                </div>
              </label>
              <label className="block">
                <span className="mb-2 block text-sm font-semibold text-slate-700">
                  Şifre
                </span>
                <input
                  required
                  name="password"
                  autoComplete="current-password"
                  type="password"
                  value={pw}
                  onChange={(e) => setPw(e.target.value)}
                  className="h-12 w-full rounded-xl border border-slate-200 px-4 text-sm outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-50"
                />
              </label>
              <div className="flex items-center justify-between text-xs">
                <label className="flex items-center gap-2 text-slate-600">
                  <input
                    type="checkbox"
                    checked={remember}
                    onChange={(event) => setRemember(event.target.checked)}
                    className="rounded border-slate-300"
                  />{" "}
                  Oturumu açık tut
                </label>
                <button
                  type="button"
                  onClick={() =>
                    notify(
                      "Şifre sıfırlama talebi sistem yöneticisine iletildi",
                      "info",
                    )
                  }
                  className="font-semibold text-blue-600"
                >
                  Şifremi unuttum
                </button>
              </div>
              <Button size="lg" className="w-full" disabled={busy}>
                {busy ? "Oturum doğrulanıyor…" : "Güvenli giriş yap"}{" "}
                <ArrowUpRight size={17} />
              </Button>
            </form>
            {isDemo && (
              <div className="mt-6 flex items-start gap-3 rounded-xl border border-blue-100 bg-blue-50/70 p-4">
                <Sparkles className="mt-0.5 shrink-0 text-blue-600" size={17} />
                <p className="text-xs leading-5 text-blue-800">
                  <b>Demo ortamı:</b> Kurum admini için ADMIN_DEMO, platform
                  yönetimi için SUPER_ADMIN kullanabilirsiniz. Şifre: demo123.
                </p>
              </div>
            )}
            <p className="mt-8 text-center text-[11px] text-slate-400">
              © 2026 CEDAŞ · Eğitim ve İş Güvenliği Sistemleri
            </p>
          </div>
        </main>
      </div>
    </div>
  );
}

function metrics(data: Bootstrap) {
  const q = data.events.filter((e) => e.eventType === "QuizAnswered"),
    correct = q.filter((e) => e.payload.isCorrect === true).length,
    mistakes = data.events.filter(
      (e) => e.eventType === "MistakeRecorded",
    ).length,
    runs = data.events.filter((e) => e.eventType === "LevelStarted").length,
    completed = data.events.filter(
      (e) => e.eventType === "LevelCompleted",
    ).length;
  return {
    accuracy: q.length ? (correct / q.length) * 100 : 0,
    mistakes,
    runs,
    completed,
    active: new Set(data.events.map((e) => e.employeeId)).size,
    questions: q.length,
  };
}
function series(data: Bootstrap) {
  const buckets = new Map<
    string,
    { label: string; date: string; accuracy: number; total: number; mistakes: number }
  >();
  data.events.forEach((e) => {
    const d = new Date(e.clientTimestamp),
      key = d.toISOString().slice(0, 10),
      b = buckets.get(key) || {
        label: d.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", timeZone: "UTC" }),
        date: key,
        accuracy: 0,
        total: 0,
        mistakes: 0,
      };
    if (e.eventType === "QuizAnswered") {
      b.total++;
      if (e.payload.isCorrect) b.accuracy++;
    }
    if (e.eventType === "MistakeRecorded") b.mistakes++;
    buckets.set(key, b);
  });
  return [...buckets.values()].sort((a, b) => a.date.localeCompare(b.date)).slice(-14).map((x) => ({
    ...x,
    accuracy: x.total ? Math.round((x.accuracy / x.total) * 100) : 0,
  }));
}
function employeeStats(data: Bootstrap, employee: Employee) {
  const ev = data.events.filter((e) => e.employeeId === employee.id),
    q = ev.filter((e) => e.eventType === "QuizAnswered"),
    correct = q.filter((e) => e.payload.isCorrect).length;
  return {
    ...employee,
    events: ev.length,
    runs: ev.filter((e) => e.eventType === "LevelStarted").length,
    accuracy: q.length ? Math.round((correct / q.length) * 100) : 0,
    mistakes: ev.filter((e) => e.eventType === "MistakeRecorded").length,
    last: ev.reduce<string | undefined>(
      (latest, event) => !latest || event.clientTimestamp > latest ? event.clientTimestamp : latest,
      undefined,
    ),
  };
}

function scenarioAnalytics(data: Bootstrap) {
  return data.content.levels.map((level) => {
    const events = data.events.filter(
      (e) => e.payload.levelId === level.emittedLevelId,
    );
    const quizzes = events.filter((e) => e.eventType === "QuizAnswered");
    const actions = events.filter(
      (e) => e.eventType === "ActionCompleted",
    ).length;
    const mistakes = events.filter(
      (e) => e.eventType === "MistakeRecorded",
    ).length;
    const started = events.filter((e) => e.eventType === "LevelStarted").length;
    const completed = events.filter(
      (e) => e.eventType === "LevelCompleted",
    ).length;
    return {
      name: level.name.split(" ").slice(0, 3).join(" "),
      accuracy: quizzes.length
        ? Math.round(
            (quizzes.filter((e) => e.payload.isCorrect).length /
              quizzes.length) *
              100,
          )
        : 0,
      completion: started ? Math.round((completed / started) * 100) : 0,
      risk: actions ? Number(((mistakes / actions) * 100).toFixed(1)) : 0,
      sample: quizzes.length,
    };
  });
}

function AdvancedAnalytics({ data }: { data: Bootstrap }) {
  const scenarios = scenarioAnalytics(data);
  const distribution = [
    { band: "0–59", count: 0 },
    { band: "60–69", count: 0 },
    { band: "70–79", count: 0 },
    { band: "80–89", count: 0 },
    { band: "90–100", count: 0 },
  ];
  data.employees
    .map((e) => employeeStats(data, e).accuracy)
    .forEach((value) => {
      const index =
        value < 60 ? 0 : value < 70 ? 1 : value < 80 ? 2 : value < 90 ? 3 : 4;
      distribution[index].count++;
    });
  const started = data.events.filter(
    (e) => e.eventType === "LevelStarted",
  ).length;
  const actions = new Set(
    data.events
      .filter((e) => e.eventType === "ActionCompleted")
      .map((e) => String(e.payload.sessionId)),
  ).size;
  const completed = data.events.filter(
    (e) => e.eventType === "LevelCompleted",
  ).length;
  const funnel = [
    { name: "Başlatıldı", value: started },
    { name: "Aksiyonlu oturum", value: actions },
    { name: "Tamamlandı", value: completed },
  ];
  return (
    <div className="mt-5 grid gap-4 xl:grid-cols-12">
      <Card className="xl:col-span-7">
        <CardHeader>
          <div>
            <CardTitle>Senaryo karşılaştırması</CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              Doğruluk ve tamamlanma; tüm değerler eventlerden hesaplanır.
            </p>
          </div>
          <Badge tone="blue">
            n = {scenarios.reduce((a, x) => a + x.sample, 0)}
          </Badge>
        </CardHeader>
        <CardContent className="h-[330px]">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart
              data={scenarios}
              margin={{ left: -12, right: 12, top: 10 }}
            >
              <CartesianGrid vertical={false} stroke="#eef2f7" />
              <XAxis
                dataKey="name"
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 11, fill: "#64748b" }}
              />
              <YAxis
                domain={[0, 100]}
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 11, fill: "#94a3b8" }}
              />
              <Tooltip
                contentStyle={{ borderRadius: 10, border: "1px solid #e2e8f0" }}
              />
              <Legend iconType="circle" wrapperStyle={{ fontSize: 12 }} />
              <Bar
                dataKey="completion"
                name="Tamamlama %"
                fill="#bfdbfe"
                radius={[6, 6, 0, 0]}
              />
              <Line
                dataKey="accuracy"
                name="Doğruluk %"
                stroke="#2563eb"
                strokeWidth={2.5}
                dot={{ r: 4, fill: "#fff" }}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
      <Card className="xl:col-span-5">
        <CardHeader>
          <div>
            <CardTitle>Çalışan başarı dağılımı</CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              Ortalama tek başına gizlenen dağılımı gösterir.
            </p>
          </div>
        </CardHeader>
        <CardContent className="h-[330px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={distribution} margin={{ left: -20, top: 10 }}>
              <CartesianGrid vertical={false} stroke="#eef2f7" />
              <XAxis
                dataKey="band"
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 11 }}
              />
              <YAxis allowDecimals={false} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{ borderRadius: 10, border: "1px solid #e2e8f0" }}
              />
              <ReferenceLine x="80–89" stroke="#2563eb" strokeDasharray="4 4" />
              <Bar
                dataKey="count"
                name="Çalışan"
                fill="#2563eb"
                radius={[6, 6, 0, 0]}
              />
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
      <Card className="xl:col-span-5">
        <CardHeader>
          <div>
            <CardTitle>Tamamlama hunisi</CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              Başlangıçtan tamamlanmaya oturum dönüşümü.
            </p>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {funnel.map((item, index) => (
            <div key={item.name}>
              <div className="mb-2 flex justify-between text-sm">
                <span className="font-medium text-slate-600">{item.name}</span>
                <b>
                  {item.value}{" "}
                  <span className="font-normal text-slate-400">
                    · %{started ? Math.round((item.value / started) * 100) : 0}
                  </span>
                </b>
              </div>
              <div className="h-3 overflow-hidden rounded bg-slate-100">
                <div
                  className="h-full rounded bg-blue-600"
                  style={{
                    width: `${started ? (item.value / started) * 100 : 0}%`,
                    opacity: 1 - index * 0.2,
                  }}
                />
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
      <Card className="xl:col-span-7">
        <CardHeader>
          <div>
            <CardTitle>Normalize risk oranı</CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              100 tamamlanan aksiyon başına hata; ham hacim yanlılığını azaltır.
            </p>
          </div>
          <Badge tone="amber">Formül doğrulandı</Badge>
        </CardHeader>
        <CardContent className="h-[230px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={[...scenarios].sort((a, b) => b.risk - a.risk)}
              layout="vertical"
              margin={{ left: 28, right: 24 }}
            >
              <CartesianGrid horizontal={false} stroke="#eef2f7" />
              <XAxis type="number" axisLine={false} tickLine={false} />
              <YAxis
                type="category"
                dataKey="name"
                width={110}
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 11 }}
              />
              <Tooltip
                formatter={(value) => [`${value} hata`, "100 aksiyon başına"]}
                contentStyle={{ borderRadius: 10, border: "1px solid #e2e8f0" }}
              />
              <Bar dataKey="risk" fill="#f59e0b" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </div>
  );
}

const telemetryEventTypes = [
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
] as const;

function TelemetryCoverage({ data }: { data: Bootstrap }) {
  const counts = new Map<string, number>();
  data.events.forEach((event) => counts.set(event.eventType, (counts.get(event.eventType) || 0) + 1));
  const covered = telemetryEventTypes.filter((type) => (counts.get(type) || 0) > 0).length;
  return (
    <Card className="mt-4">
      <CardHeader>
        <div>
          <CardTitle>Telemetri kapsamı</CardTitle>
          <p className="mt-1 text-xs text-slate-400">
            Unity sözleşmesindeki 11 olay türünün veri akışındaki görünürlüğü
          </p>
        </div>
        <Badge tone={covered === telemetryEventTypes.length ? "green" : "amber"}>
          {covered}/{telemetryEventTypes.length} aktif
        </Badge>
      </CardHeader>
      <CardContent className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-6">
        {telemetryEventTypes.map((type) => {
          const count = counts.get(type) || 0;
          return (
            <div key={type} className={cn("rounded-xl border px-3 py-3", count ? "border-emerald-100 bg-emerald-50/50" : "border-slate-200 bg-slate-50")}>
              <div className="truncate text-[11px] font-semibold text-slate-600" title={type}>{type}</div>
              <div className={cn("mt-1 text-lg font-bold", count ? "text-emerald-700" : "text-slate-400")}>
                {fmt.format(count)}
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

function StatCard({
  label,
  value,
  detail,
  icon: Icon,
  tone = "blue",
  delta,
}: {
  label: string;
  value: string;
  detail: string;
  icon: typeof Activity;
  tone?: string;
  delta?: string;
}) {
  const colors: Record<string, string> = {
    blue: "bg-blue-50 text-blue-600",
    green: "bg-emerald-50 text-emerald-600",
    amber: "bg-amber-50 text-amber-600",
    violet: "bg-violet-50 text-violet-600",
  };
  return (
    <Card className="group transition hover:-translate-y-0.5 hover:shadow-lg hover:shadow-slate-200/50">
      <CardContent className="p-5">
        <div className="flex items-start justify-between">
          <div
            className={cn(
              "grid size-10 place-items-center rounded-xl",
              colors[tone],
            )}
          >
            <Icon size={19} />
          </div>
          {delta && (
            <Badge tone={delta.startsWith("+") ? "green" : "red"}>
              {delta}
            </Badge>
          )}
        </div>
        <div className="mt-5 text-2xl font-bold tracking-[-.03em] text-slate-950">
          {value}
        </div>
        <div className="mt-1 text-sm font-medium text-slate-700">{label}</div>
        <div className="mt-1 text-xs text-slate-400">{detail}</div>
      </CardContent>
    </Card>
  );
}

function PageHead({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children?: React.ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div>
        <h1 className="text-2xl font-bold tracking-[-.03em] text-slate-950">
          {title}
        </h1>
        <p className="mt-1 text-sm text-slate-500">{description}</p>
      </div>
      {children && <div className="flex gap-2">{children}</div>}
    </div>
  );
}

function Dashboard({ data }: { data: Bootstrap }) {
  const m = metrics(data),
    chart = series(data),
    people = data.employees
      .map((e) => employeeStats(data, e))
      .sort((a, b) => b.mistakes - a.mistakes)
      .slice(0, 5);
  return (
    <div className="fade-up">
      <PageHead
        title="Genel Bakış"
        description="Kurum genelindeki eğitim performansı ve operasyonel risk görünümü."
      >
        <Button variant="outline" onClick={() => navigateTo("reports")}>
          <Download size={16} />
          Rapor al
        </Button>
        <Dialog>
          <DialogTrigger asChild>
            <Button>
              <Sparkles size={16} />
              İçgörü oluştur
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogTitle>Akıllı yönetim içgörüsü</DialogTitle>
            <div className="mt-5 space-y-3">
              {people.slice(0, 3).map((person) => (
                <div key={person.id} className="rounded-xl border p-4">
                  <div className="flex justify-between">
                    <b>{person.name}</b>
                    <Badge tone={person.mistakes > 8 ? "red" : "amber"}>
                      {person.mistakes} hata
                    </Badge>
                  </div>
                  <p className="mt-2 text-sm text-slate-500">
                    %{person.accuracy} doğruluk ve {person.runs} deneme.{" "}
                    {person.mistakes > 8
                      ? "Tekrar eğitimi öneriliyor."
                      : "Yakın takip öneriliyor."}
                  </p>
                </div>
              ))}
            </div>
            <Button
              className="mt-5"
              onClick={() => {
                navigateTo("risks");
                notify("Risk analizi açıldı", "info");
              }}
            >
              Risk analizine git
            </Button>
          </DialogContent>
        </Dialog>
      </PageHead>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Aktif çalışan"
          value={String(m.active)}
          detail="Son 30 günlük benzersiz kullanıcı"
          icon={Users}
        />
        <StatCard
          label="Genel doğruluk"
          value={`${m.accuracy.toFixed(1)}%`}
          detail={`${fmt.format(m.questions)} quiz yanıtı`}
          icon={Target}
          tone="green"
        />
        <StatCard
          label="Tamamlanan eğitim"
          value={String(m.completed)}
          detail={`${m.runs} toplam deneme`}
          icon={GraduationCap}
          tone="violet"
        />
        <StatCard
          label="Kayıtlı hata"
          value={String(m.mistakes)}
          detail="İncelenmesi gereken olay"
          icon={AlertTriangle}
          tone="amber"
        />
      </div>
      <TelemetryCoverage data={data} />
      <div className="mt-4 grid gap-4 xl:grid-cols-[1.6fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Performans eğilimi</CardTitle>
              <p className="mt-1 text-xs text-slate-400">
                Günlük quiz doğruluğu ve hata hareketi
              </p>
            </div>
            <Badge tone={data.IS_MOCK ? "amber" : "green"}>
              {data.IS_MOCK ? "Demo veri" : "Canlı veri"}
            </Badge>
          </CardHeader>
          <CardContent className="h-[320px]">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={chart} margin={{ left: -20, right: 6, top: 15 }}>
                <defs>
                  <linearGradient id="fillBlue" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#2563eb" stopOpacity={0.22} />
                    <stop offset="100%" stopColor="#2563eb" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid vertical={false} stroke="#eef2f7" />
                <XAxis
                  dataKey="label"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: "#94a3b8" }}
                />
                <YAxis
                  domain={[0, 100]}
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: "#94a3b8" }}
                />
                <Tooltip
                  contentStyle={{
                    borderRadius: 12,
                    border: "1px solid #e2e8f0",
                  }}
                />
                <Area
                  type="monotone"
                  dataKey="accuracy"
                  name="Doğruluk %"
                  stroke="#2563eb"
                  strokeWidth={2.5}
                  fill="url(#fillBlue)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Öncelikli takip</CardTitle>
              <p className="mt-1 text-xs text-slate-400">
                Hata yoğunluğuna göre çalışanlar
              </p>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigateTo("employees")}
              aria-label="Tüm çalışanları aç"
            >
              <MoreHorizontal size={18} />
            </Button>
          </CardHeader>
          <CardContent className="space-y-1">
            {people.map((p, i) => (
              <div
                key={p.id}
                className="flex items-center gap-3 rounded-xl px-2 py-3 hover:bg-slate-50"
              >
                <div className="grid size-9 shrink-0 place-items-center rounded-full bg-slate-100 text-xs font-bold text-slate-600">
                  {p.name
                    .split(" ")
                    .map((x) => x[0])
                    .join("")
                    .slice(0, 2)}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-semibold">{p.name}</div>
                  <div className="text-xs text-slate-400">{p.department}</div>
                </div>
                <div className="text-right">
                  <Badge tone={i < 2 ? "red" : "amber"}>
                    {p.mistakes} hata
                  </Badge>
                  <div className="mt-1 text-[10px] text-slate-400">
                    %{p.accuracy} doğruluk
                  </div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Senaryo performansı</CardTitle>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigateTo("scenarios")}
            >
              Tümünü gör
            </Button>
          </CardHeader>
          <CardContent className="h-[250px]">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={scenarioAnalytics(data).map((item) => ({
                  name: item.name,
                  score: item.accuracy,
                }))}
              >
                <CartesianGrid vertical={false} stroke="#eef2f7" />
                <XAxis
                  dataKey="name"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: "#64748b" }}
                />
                <YAxis hide domain={[0, 100]} />
                <Tooltip />
                <Bar dataKey="score" radius={[8, 8, 0, 0]}>
                  {[0, 1, 2].map((i) => (
                    <Cell key={i} fill={["#2563eb", "#8b5cf6", "#14b8a6"][i]} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
        <Card className="border-blue-100 bg-gradient-to-br from-blue-600 to-blue-700 text-white">
          <CardContent className="p-6">
            <div className="grid size-11 place-items-center rounded-xl bg-white/15">
              <Sparkles size={21} />
            </div>
            <h3 className="mt-6 text-lg font-bold">Akıllı değerlendirme</h3>
            <p className="mt-2 text-sm leading-6 text-blue-100">
              {data.IS_MOCK
                ? "Bu değerlendirme deterministik demo verisiyle hazırlanmıştır; üretim kararı için kullanılmamalıdır."
                : `${m.active} çalışanın ${m.questions} quiz yanıtında genel doğruluk %${m.accuracy.toFixed(1)}. ${m.mistakes} hata kaydı takip bekliyor.`}
            </p>
            <div className="mt-6 space-y-3">
              {[
                ["Güçlü alan", "KKD hazırlığı"],
                ["İyileştirme", "Pano enerjilendirme"],
                ["Öneri", "EMP-1045 tekrar eğitimi"],
              ].map(([a, b]) => (
                <div key={a} className="border-t border-white/15 pt-3">
                  <span className="text-[10px] uppercase tracking-wider text-blue-200">
                    {a}
                  </span>
                  <p className="mt-1 text-sm font-semibold">{b}</p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Employees({ data }: { data: Bootstrap }) {
  const [query, setQuery] = useState("");
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [department, setDepartment] = useState("");
  const [location, setLocation] = useState("");
  const [personId, setPersonId] = useState("");
  const [minMistakes, setMinMistakes] = useState("");
  const [maxMistakes, setMaxMistakes] = useState("");
  const rows = data.employees
    .map((e) => employeeStats(data, e))
    .filter((e) => (e.name + e.id).toLowerCase().includes(query.toLowerCase()))
    .filter((e) => !personId || e.id === personId)
    .filter((e) => !department || e.department === department)
    .filter((e) => !location || e.location === location)
    .filter((e) => minMistakes === "" || e.mistakes >= Number(minMistakes))
    .filter((e) => maxMistakes === "" || e.mistakes <= Number(maxMistakes));
  const activeFilterCount = [
    personId,
    department,
    location,
    minMistakes,
    maxMistakes,
  ].filter((value) => value !== "").length;
  const exportRows = () =>
    downloadCsv("cedas-calisanlar.csv", [
      ["Çalışan", "ID", "Departman", "Konum", "Deneme", "Doğruluk", "Hata"],
      ...rows.map((p) => [
        p.name,
        p.id,
        p.department || "",
        p.location || "",
        p.runs,
        p.accuracy,
        p.mistakes,
      ]),
    ]);
  return (
    <div className="fade-up">
      <PageHead
        title="Çalışanlar"
        description="Eğitim performansını çalışan bazında inceleyin."
      >
        <Button
          variant="outline"
          onClick={() => setFiltersOpen((value) => !value)}
        >
          <Filter size={16} />
          Filtrele
          {activeFilterCount > 0 && (
            <Badge tone="blue" className="ml-1 px-1.5 py-0.5">
              {activeFilterCount}
            </Badge>
          )}
        </Button>
        <Button onClick={exportRows}>
          <Download size={16} />
          Dışa aktar
        </Button>
      </PageHead>
      {filtersOpen && (
        <Card className="mb-4">
          <CardContent className="flex flex-wrap items-end gap-2.5 p-3">
            <label className="min-w-52 flex-1">
              <span className="mb-1 block text-xs font-semibold text-slate-500">
                Personel
              </span>
              <select
                value={personId}
                onChange={(e) => setPersonId(e.target.value)}
                className="compact-control"
              >
                <option value="">Tüm personeller</option>
                {[...data.employees]
                  .sort((a, b) => a.name.localeCompare(b.name, "tr"))
                  .map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.name} · {person.id}
                    </option>
                  ))}
              </select>
            </label>
            <label className="min-w-44">
              <span className="mb-1 block text-xs font-semibold text-slate-500">
                Departman
              </span>
              <select
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
                className="compact-control"
              >
                <option value="">Tümü</option>
                {[
                  ...new Set(
                    data.employees.map((e) => e.department).filter(Boolean),
                  ),
                ].map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
            <label className="min-w-44">
              <span className="mb-1 block text-xs font-semibold text-slate-500">
                Konum
              </span>
              <select
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                className="compact-control"
              >
                <option value="">Tümü</option>
                {[
                  ...new Set(
                    data.employees.map((e) => e.location).filter(Boolean),
                  ),
                ].map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
            <label className="w-32">
              <span className="mb-1 block text-xs font-semibold text-slate-500">
                Minimum hata
              </span>
              <input
                type="number"
                min="0"
                inputMode="numeric"
                value={minMistakes}
                onChange={(e) => setMinMistakes(e.target.value)}
                placeholder="0"
                className="compact-control"
              />
            </label>
            <label className="w-32">
              <span className="mb-1 block text-xs font-semibold text-slate-500">
                Maksimum hata
              </span>
              <input
                type="number"
                min="0"
                inputMode="numeric"
                value={maxMistakes}
                onChange={(e) => setMaxMistakes(e.target.value)}
                placeholder="Sınırsız"
                className="compact-control"
              />
            </label>
            <Button
              variant="ghost"
              onClick={() => {
                setPersonId("");
                setDepartment("");
                setLocation("");
                setMinMistakes("");
                setMaxMistakes("");
                setQuery("");
              }}
            >
              Filtreleri temizle
            </Button>
          </CardContent>
        </Card>
      )}
      <Card>
        <CardHeader className="items-center">
          <div className="relative w-full max-w-sm">
            <Search
              className="absolute left-3 top-2.5 text-slate-400"
              size={17}
            />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Ad veya çalışan ID ara"
              className="compact-control pl-9"
            />
          </div>
          <Badge>{rows.length} çalışan</Badge>
        </CardHeader>
        <CardContent className="overflow-x-auto p-0">
          <table className="data-table min-w-[800px]">
            <thead>
              <tr>
                {[
                  "Çalışan",
                  "Departman",
                  "Konum",
                  "Deneme",
                  "Doğruluk",
                  "Hata",
                  "Son aktivite",
                  "",
                ].map((x) => (
                  <th key={x}>{x}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.id} className="group hover:bg-slate-50/70">
                  <td>
                    <div className="flex items-center gap-2.5">
                      <div className="grid size-8 place-items-center rounded-full bg-blue-50 text-[11px] font-bold text-blue-700">
                        {p.name
                          .split(" ")
                          .map((x) => x[0])
                          .join("")
                          .slice(0, 2)}
                      </div>
                      <div>
                        <b className="text-sm">{p.name}</b>
                        <div className="text-xs text-slate-400">{p.id}</div>
                      </div>
                    </div>
                  </td>
                  <td className="text-slate-600">{p.department}</td>
                  <td className="text-slate-600">{p.location}</td>
                  <td className="font-semibold">{p.runs}</td>
                  <td>
                    <div className="flex items-center gap-2">
                      <div className="h-1.5 w-16 overflow-hidden rounded-full bg-slate-100">
                        <div
                          className="h-full rounded-full bg-emerald-500"
                          style={{ width: `${p.accuracy}%` }}
                        />
                      </div>
                      <span className="text-xs font-semibold">
                        %{p.accuracy}
                      </span>
                    </div>
                  </td>
                  <td>
                    <Badge
                      tone={
                        p.mistakes > 8
                          ? "red"
                          : p.mistakes > 3
                            ? "amber"
                            : "green"
                      }
                    >
                      {p.mistakes}
                    </Badge>
                  </td>
                  <td className="text-xs text-slate-500">
                    {p.last
                      ? new Date(p.last).toLocaleDateString("tr-TR")
                      : "—"}
                  </td>
                  <td>
                    <Dialog>
                      <DialogTrigger asChild>
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label={`${p.name} detayını aç`}
                        >
                          <MoreHorizontal size={17} />
                        </Button>
                      </DialogTrigger>
                      <DialogContent>
                        <DialogTitle>{p.name}</DialogTitle>
                        <div className="mt-5 grid grid-cols-3 gap-3">
                          {[
                            ["Deneme", p.runs],
                            ["Doğruluk", `%${p.accuracy}`],
                            ["Hata", p.mistakes],
                          ].map(([label, value]) => (
                            <div
                              key={label}
                              className="rounded-xl bg-slate-50 p-4"
                            >
                              <span className="text-xs text-slate-500">
                                {label}
                              </span>
                              <b className="mt-1 block text-xl">{value}</b>
                            </div>
                          ))}
                        </div>
                        <div className="mt-5 rounded-xl border p-4 text-sm text-slate-600">
                          <b>Çalışan kapsamı</b>
                          <p className="mt-2">
                            {p.department} · {p.location} · Son aktivite{" "}
                            {p.last
                              ? new Date(p.last).toLocaleString("tr-TR")
                              : "yok"}
                          </p>
                        </div>
                        <Button
                          className="mt-5"
                          onClick={() =>
                            downloadCsv(`${p.id}-performans.csv`, [
                              ["Metrik", "Değer"],
                              ["Deneme", p.runs],
                              ["Doğruluk", p.accuracy],
                              ["Hata", p.mistakes],
                            ])
                          }
                        >
                          <Download size={16} />
                          Çalışan raporunu indir
                        </Button>
                      </DialogContent>
                    </Dialog>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardContent>
      </Card>
    </div>
  );
}

function Scenarios({ data }: { data: Bootstrap }) {
  const analytics = scenarioAnalytics(data);
  return (
    <div className="fade-up">
      <PageHead
        title="Senaryolar"
        description="Eğitim içeriklerinin zorluk ve başarı karşılaştırması."
      >
        <Dialog>
          <DialogTrigger asChild>
            <Button>
              <BookOpen size={16} />
              Senaryo şablonu
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogTitle>Yeni senaryo hazırlığı</DialogTitle>
            <p className="mt-3 text-sm leading-6 text-slate-600">
              Senaryo içeriği Unity ScriptableObject kataloğundan gelir.
              Aşağıdaki şablonu içerik ekibine verip yeni level, sequence ve
              action kimliklerini tanımlayabilirsiniz.
            </p>
            <Button
              className="mt-5"
              onClick={() =>
                downloadCsv("yeni-senaryo-sablonu.csv", [
                  [
                    "levelId",
                    "levelName",
                    "sequenceId",
                    "sequenceName",
                    "actionId",
                    "actionType",
                  ],
                  [
                    "level-4",
                    "Yeni Senaryo",
                    "SEQ-1",
                    "Hazırlık",
                    "ACTION-1",
                    "quiz",
                  ],
                ])
              }
            >
              <Download size={16} />
              Şablonu indir
            </Button>
          </DialogContent>
        </Dialog>
      </PageHead>
      <div className="grid gap-4 lg:grid-cols-3">
        {data.content.levels.map((l, i) => {
          const stats = analytics[i];
          const levelEvents = data.events.filter(
            (event) => event.payload.levelId === l.emittedLevelId,
          );
          const activeEmployees = new Set(
            levelEvents.map((event) => event.employeeId),
          ).size;
          const mistakes = levelEvents.filter(
            (event) => event.eventType === "MistakeRecorded",
          ).length;
          return (
            <Card
              key={l.emittedLevelId}
              className="overflow-hidden transition hover:shadow-lg"
            >
              <div
                className={cn(
                  "h-2",
                  i === 0
                    ? "bg-blue-600"
                    : i === 1
                      ? "bg-violet-500"
                      : "bg-teal-500",
                )}
              />
              <CardContent className="p-6">
                <div className="flex items-start justify-between">
                  <div
                    className={cn(
                      "grid size-11 place-items-center rounded-xl",
                      i === 0
                        ? "bg-blue-50 text-blue-600"
                        : i === 1
                          ? "bg-violet-50 text-violet-600"
                          : "bg-teal-50 text-teal-600",
                    )}
                  >
                    <ClipboardCheck size={21} />
                  </div>
                  <Badge tone="green">Aktif</Badge>
                </div>
                <h3 className="mt-5 text-base font-bold">{l.name}</h3>
                <p className="mt-2 text-sm leading-6 text-slate-500">
                  Elektrik dağıtım operasyonlarında güvenli çalışma adımları ve
                  uygulamalı değerlendirme.
                </p>
                <div className="mt-6 grid grid-cols-3 border-y py-4 text-center">
                  <div>
                    <b className="block text-lg">{activeEmployees}</b>
                    <span className="text-[10px] text-slate-400">Çalışan</span>
                  </div>
                  <div className="border-x">
                    <b className="block text-lg">%{stats.accuracy}</b>
                    <span className="text-[10px] text-slate-400">Başarı</span>
                  </div>
                  <div>
                    <b className="block text-lg">{mistakes}</b>
                    <span className="text-[10px] text-slate-400">Hata</span>
                  </div>
                </div>
                <Dialog>
                  <DialogTrigger asChild>
                    <Button variant="outline" className="mt-5 w-full">
                      Senaryoyu incele <ArrowUpRight size={15} />
                    </Button>
                  </DialogTrigger>
                  <DialogContent>
                    <DialogTitle>{l.name}</DialogTitle>
                    <div className="mt-5 grid grid-cols-3 gap-3">
                      {[
                        ["Aktif çalışan", activeEmployees],
                        ["Doğruluk", `%${stats.accuracy}`],
                        ["Tamamlama", `%${stats.completion}`],
                      ].map(([label, value]) => (
                        <div key={label} className="rounded-xl bg-slate-50 p-4">
                          <span className="text-xs text-slate-500">
                            {label}
                          </span>
                          <b className="mt-1 block text-xl">{value}</b>
                        </div>
                      ))}
                    </div>
                    <div className="mt-5 rounded-xl border p-4">
                      <b className="text-sm">Normalize risk</b>
                      <p className="mt-2 text-sm text-slate-500">
                        100 aksiyon başına {stats.risk} hata · {stats.sample}{" "}
                        quiz örneği.
                      </p>
                    </div>
                    <Button
                      className="mt-5"
                      onClick={() =>
                        downloadCsv(`${l.emittedLevelId}-rapor.csv`, [
                          [
                            "Senaryo",
                            "Doğruluk",
                            "Tamamlama",
                            "Risk",
                            "Örneklem",
                          ],
                          [
                            l.name,
                            stats.accuracy,
                            stats.completion,
                            stats.risk,
                            stats.sample,
                          ],
                        ])
                      }
                    >
                      <Download size={16} />
                      Senaryo raporunu indir
                    </Button>
                  </DialogContent>
                </Dialog>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

function Risks({ data }: { data: Bootstrap }) {
  const [threshold, setThreshold] = useState(8),
    [filtersOpen, setFiltersOpen] = useState(false);
  const people = data.employees
      .map((e) => employeeStats(data, e))
      .sort((a, b) => b.mistakes - a.mistakes),
    chart = people
      .slice(0, 8)
      .map((p) => ({ name: p.name.split(" ")[0], hata: p.mistakes })),
    totals = metrics(data),
    completionRate = totals.runs ? (totals.completed / totals.runs) * 100 : 0;
  return (
    <div className="fade-up">
      <PageHead
        title="Risk Analizi"
        description="Tekrarlanan hataları ve dikkat gerektiren alanları önceliklendirin."
      >
        <Button
          variant="outline"
          onClick={() => setFiltersOpen((value) => !value)}
        >
          <Filter size={16} />
          Risk filtresi
        </Button>
      </PageHead>
      {filtersOpen && (
        <Card className="mb-4">
          <CardContent className="flex items-center gap-3 p-3">
            <label className="flex-1">
              <span className="mb-1 flex justify-between text-xs font-semibold text-slate-600">
                <span>Yüksek risk eşiği</span>
                <b>{threshold} hata</b>
              </span>
              <input
                type="range"
                min="1"
                max="20"
                value={threshold}
                onChange={(e) => setThreshold(Number(e.target.value))}
                className="w-full"
              />
            </label>
            <Button variant="ghost" onClick={() => setThreshold(8)}>
              Varsayılana dön
            </Button>
          </CardContent>
        </Card>
      )}
      <div className="grid gap-4 md:grid-cols-3">
        <StatCard
          label="Yüksek riskli çalışan"
          value={String(people.filter((x) => x.mistakes > threshold).length)}
          detail={`${threshold} üzeri hata kaydı`}
          icon={AlertTriangle}
          tone="amber"
        />
        <StatCard
          label="Tekrarlanan hata"
          value={String(metrics(data).mistakes)}
          detail="Yanlış cevap ve işlem"
          icon={ArrowDownRight}
          tone="violet"
        />
        <StatCard
          label="Tamamlama oranı"
          value={`${completionRate.toFixed(1)}%`}
          detail={`${totals.completed}/${totals.runs} oturum`}
          icon={TrendingUp}
          tone="green"
        />
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-[1.35fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Çalışan bazlı hata yoğunluğu</CardTitle>
            <Badge tone="amber">Takip gerekli</Badge>
          </CardHeader>
          <CardContent className="h-[350px]">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chart} layout="vertical" margin={{ left: 10 }}>
                <CartesianGrid horizontal={false} stroke="#eef2f7" />
                <XAxis type="number" axisLine={false} tickLine={false} />
                <YAxis
                  dataKey="name"
                  type="category"
                  axisLine={false}
                  tickLine={false}
                  width={70}
                  tick={{ fontSize: 11 }}
                />
                <Tooltip />
                <Bar dataKey="hata" radius={[0, 7, 7, 0]} fill="#f59e0b" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Risk aksiyonları</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {[
              [
                "EMP-1045 tekrar eğitimi",
                "Pano enerjilendirme adımlarında 12 hata",
                "red",
              ],
              [
                "Seviye 2 içerik kontrolü",
                "Doğruluk kurum ortalamasının 9 puan altında",
                "amber",
              ],
              ["KKD adımı başarılı", "Son 30 günde %18 iyileşme", "green"],
            ].map(([a, b, t]) => (
              <div key={a} className="rounded-xl border p-4">
                <div className="flex items-start gap-3">
                  <div
                    className={cn(
                      "mt-1 size-2 rounded-full",
                      t === "red"
                        ? "bg-red-500"
                        : t === "amber"
                          ? "bg-amber-500"
                          : "bg-emerald-500",
                    )}
                  />
                  <div>
                    <b className="text-sm">{a}</b>
                    <p className="mt-1 text-xs leading-5 text-slate-500">{b}</p>
                    <Dialog>
                      <DialogTrigger asChild>
                        <button className="mt-3 text-xs font-semibold text-blue-600">
                          Detayı incele →
                        </button>
                      </DialogTrigger>
                      <DialogContent>
                        <DialogTitle>{a}</DialogTitle>
                        <p className="mt-3 text-sm leading-6 text-slate-600">
                          {b}
                        </p>
                        <div className="mt-5 rounded-xl bg-amber-50 p-4 text-sm text-amber-800">
                          Önerilen aksiyon: ilgili çalışan ve senaryo
                          kayıtlarını inceleyin, tekrar eğitimi planlayın ve
                          sonucu bir sonraki rapor döneminde karşılaştırın.
                        </div>
                        <Button
                          className="mt-5"
                          onClick={() => {
                            navigateTo("employees");
                            notify("İlgili çalışan listesi açıldı", "info");
                          }}
                        >
                          Çalışanları incele
                        </Button>
                      </DialogContent>
                    </Dialog>
                  </div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Reports({ data }: { data: Bootstrap }) {
  const m = metrics(data);
  const requiredFields = data.events.length * 3;
  const presentFields = data.events.reduce(
    (sum, event) =>
      sum +
      (event.payload.sessionId ? 1 : 0) +
      (event.payload.levelId ? 1 : 0) +
      (event.employeeId ? 1 : 0),
    0,
  );
  const qualityScore = data.quality?.received
    ? Math.round((data.quality.accepted / data.quality.received) * 100)
    : requiredFields
      ? Math.round((presentFields / requiredFields) * 100)
      : 0;
  function download() {
    const rows = [
      ["Çalışan", "ID", "Departman", "Deneme", "Doğruluk", "Hata"],
      ...data.employees.map((e) => {
        const p = employeeStats(data, e);
        return [
          p.name,
          p.id,
          p.department || "",
          p.runs,
          p.accuracy,
          p.mistakes,
        ];
      }),
    ];
    downloadCsv("cedas-egitim-raporu.csv", rows);
  }
  const templates = [
    [
      "Yönetim Özeti",
      "Kurum KPI’ları, eğilimler ve öncelikli aksiyonlar",
      BarChart3,
      "blue",
    ],
    [
      "Çalışan Performansı",
      "Çalışan bazlı gelişim ve karşılaştırma raporu",
      Users,
      "violet",
    ],
    [
      "Senaryo Analizi",
      "Senaryo zorlukları ve başarı kırılımı",
      BookOpen,
      "green",
    ],
    [
      "Risk ve Hata",
      "Tekrarlayan hatalar ve risk kümeleri",
      ShieldCheck,
      "amber",
    ],
  ] as const;
  return (
    <div className="fade-up">
      <PageHead
        title="Rapor Merkezi"
        description="Yönetim kararları için hazır, dışa aktarılabilir analiz paketleri."
      >
        <Button variant="outline" onClick={() => window.print()}>
          <FileBarChart size={16} />
          Yazdır
        </Button>
        <Button onClick={download}>
          <Download size={16} />
          Kurum CSV
        </Button>
      </PageHead>
      <Card className="overflow-hidden border-0 bg-gradient-to-r from-blue-600 to-indigo-600 text-white">
        <CardContent className="flex flex-col justify-between gap-8 p-8 md:flex-row md:items-center">
          <div>
            <Badge className="bg-white/15 text-white">Raporlama Merkezi</Badge>
            <h2 className="mt-4 text-2xl font-bold tracking-tight">
              Veriyi yönetsel içgörüye dönüştürün.
            </h2>
            <p className="mt-2 max-w-xl text-sm leading-6 text-blue-100">
              Aktif kapsamdaki {fmt.format(data.events.length)} kayıt rapor
              üretimine hazır. Şablon seçin, önizleyin ve paylaşın.
            </p>
          </div>
          <div className="rounded-2xl border border-white/15 bg-white/10 px-8 py-5 text-center backdrop-blur">
            <span className="text-xs text-blue-100">Veri kapsama skoru</span>
            <b className="mt-1 block text-4xl">{qualityScore}</b>
            <span className="text-xs text-blue-100">
              /100 · {qualityScore >= 95 ? "Güçlü" : qualityScore >= 80 ? "İzlenmeli" : "Müdahale gerekli"}
            </span>
          </div>
        </CardContent>
      </Card>
      <AdvancedAnalytics data={data} />
      <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {templates.map(([title, desc, Icon, tone]) => (
          <Card key={title} className="group hover:shadow-lg">
            <CardContent className="p-5">
              <div className="flex items-center justify-between">
                <div className="grid size-10 place-items-center rounded-xl bg-slate-100 text-slate-700 group-hover:bg-blue-50 group-hover:text-blue-600">
                  <Icon size={19} />
                </div>
                <Badge
                  tone={
                    tone === "green"
                      ? "green"
                      : tone === "amber"
                        ? "amber"
                        : "blue"
                  }
                >
                  Hazır
                </Badge>
              </div>
              <h3 className="mt-5 text-sm font-bold">{title}</h3>
              <p className="mt-2 min-h-10 text-xs leading-5 text-slate-500">
                {desc}
              </p>
              <Dialog>
                <DialogTrigger asChild>
                  <Button variant="outline" className="mt-5 w-full">
                    Önizle
                  </Button>
                </DialogTrigger>
                <DialogContent>
                  <DialogTitle>{title}</DialogTitle>
                  <div className="mt-5 rounded-xl border bg-slate-50 p-6">
                    <div className="flex justify-between border-b pb-5">
                      <Logo />
                      <Badge tone="green">Hazır</Badge>
                    </div>
                    <div className="mt-6 grid grid-cols-4 gap-3">
                      {[
                        ["Aktif", m.active],
                        ["Deneme", m.runs],
                        ["Doğruluk", `${m.accuracy.toFixed(0)}%`],
                        ["Hata", m.mistakes],
                      ].map(([l, v]) => (
                        <div className="rounded-xl bg-white p-4" key={l}>
                          <span className="text-xs text-slate-400">{l}</span>
                          <b className="mt-1 block text-xl">{v}</b>
                        </div>
                      ))}
                    </div>
                    <p className="mt-6 text-sm leading-6 text-slate-600">
                      Seçili rapor, demo veri sağlayıcısındaki güncel eğitim
                      kayıtları üzerinden oluşturulmuştur. Canlı bağlantıda aynı
                      şablon gerçek PlayFab verileriyle çalışır.
                    </p>
                  </div>
                  <Button className="mt-5" onClick={download}>
                    <Download size={16} />
                    CSV olarak indir
                  </Button>
                </DialogContent>
              </Dialog>
            </CardContent>
          </Card>
        ))}
      </div>
      <div className="mt-5 grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Son oluşturulan raporlar</CardTitle>
            <Badge tone="green">3 hazır</Badge>
          </CardHeader>
          <CardContent className="space-y-2">
            {[
              "Haftalık İSG Yönetim Özeti",
              "Çalışan Performans Paketi",
              "Senaryo Risk Analizi",
            ].map((x, i) => (
              <div
                className="flex items-center gap-3 rounded-xl border p-3"
                key={x}
              >
                <div className="grid size-9 place-items-center rounded-lg bg-blue-50 text-blue-600">
                  <FileBarChart size={17} />
                </div>
                <div className="flex-1">
                  <b className="text-sm">{x}</b>
                  <div className="text-xs text-slate-400">
                    {28 - i * 7} Tem 2026 · PDF
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`${x} raporunu indir`}
                  onClick={download}
                >
                  <Download size={16} />
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Planlı gönderimler</CardTitle>
            <Badge tone="blue">Otomatik</Badge>
          </CardHeader>
          <CardContent className="space-y-2">
            {[
              ["Pazartesi · 09:00", "İSG yönetim ekibi"],
              ["Ayın 1’i · 08:30", "Eğitim koordinatörleri"],
              ["Cuma · 17:00", "Saha yöneticileri"],
            ].map(([a, b]) => (
              <div
                className="flex items-center gap-3 rounded-xl border p-3"
                key={a}
              >
                <div className="grid size-9 place-items-center rounded-lg bg-violet-50 text-violet-600">
                  <Clock3 size={17} />
                </div>
                <div className="flex-1">
                  <b className="text-sm">{a}</b>
                  <div className="text-xs text-slate-400">{b}</div>
                </div>
                <span className="size-2 rounded-full bg-emerald-500" />
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function AccessManagement({
  user,
  data,
}: {
  user: SessionUser;
  data: Bootstrap;
}) {
  const roles = [
    {
      role: "Süper Admin",
      key: "super_admin",
      scope: "Tüm kurumlar ve platform",
      permissions: "Entegrasyon, kullanıcı, global audit, tüm analitik",
    },
    {
      role: "Admin",
      key: "admin",
      scope: "Yalnız kendi kurumu",
      permissions: "Kurum analitiği, kullanıcılar, entegrasyon, tenant audit",
    },
    {
      role: "Denetçi",
      key: "inspector",
      scope: "Atanmış ekip ve lokasyon",
      permissions: "Ekip analitiği, çalışan görüntüleme, rapor export",
    },
    {
      role: "Çalışan",
      key: "trainee",
      scope: "Yalnız kendi verisi",
      permissions: "Kendi performansı ve eğitim sonuçları",
    },
  ];
  return (
    <div className="fade-up">
      <PageHead
        title="Erişim Yönetimi"
        description="Roller, veri kapsamları ve yetki sınırları."
      >
        <Badge tone="blue">
          {user.role === "super_admin" ? "Platform yetkisi" : "Kurum yetkisi"}
        </Badge>
      </PageHead>
      <div className="grid gap-4 lg:grid-cols-4">
        {roles.map((item, index) => (
          <Card
            key={item.key}
            className={cn(
              index === 0 && user.role !== "super_admin" && "opacity-55",
            )}
          >
            <CardContent className="p-5">
              <div className="flex items-start justify-between">
                <div className="grid size-10 place-items-center rounded-xl bg-blue-50 text-blue-600">
                  <KeyRound size={18} />
                </div>
                <Badge tone={index < 2 ? "blue" : "slate"}>{item.key}</Badge>
              </div>
              <h3 className="mt-5 font-bold">{item.role}</h3>
              <p className="mt-1 text-xs font-semibold text-slate-500">
                {item.scope}
              </p>
              <p className="mt-3 text-xs leading-5 text-slate-500">
                {item.permissions}
              </p>
            </CardContent>
          </Card>
        ))}
      </div>
      <Card className="mt-5">
        <CardHeader>
          <div>
            <CardTitle>Kullanıcı ve rol atamaları</CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              Aktif tenant içindeki hesaplar. Süper Admin rolünü yalnız platform
              yöneticisi atayabilir.
            </p>
          </div>
          <Badge>{data.managers.length + data.employees.length} hesap</Badge>
        </CardHeader>
        <CardContent className="overflow-x-auto p-0">
          <table className="data-table min-w-[720px]">
            <thead>
              <tr>
                {["Kullanıcı", "Rol", "Veri kapsamı", "Durum"].map((x) => (
                  <th key={x}>{x}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {[...data.managers, ...data.employees.slice(0, 8)].map(
                (person) => (
                  <tr key={person.id}>
                    <td>
                      <b className="text-sm">{person.name}</b>
                      <div className="text-xs text-slate-400">{person.id}</div>
                    </td>
                    <td>
                      <Badge
                        tone={person.role.includes("admin") ? "blue" : "slate"}
                      >
                        {person.role}
                      </Badge>
                    </td>
                    <td className="text-slate-500">
                      {person.role.includes("admin")
                        ? "Kurum geneli"
                        : "Kendi kayıtları"}
                    </td>
                    <td>
                      <Badge tone="green">Aktif</Badge>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </CardContent>
      </Card>
    </div>
  );
}

type AuditItem = {
  at: string;
  action: string;
  subject: string;
  actor?: string;
  tenantId?: string;
  role?: string;
  ip?: string;
};
function AuditLogs() {
  const [items, setItems] = useState<AuditItem[]>([]),
    [loading, setLoading] = useState(true);
  useEffect(() => {
    fetch("/api/v1/audit", { credentials: "same-origin" })
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then((x) => setItems(x.items || []))
      .finally(() => setLoading(false));
  }, []);
  return (
    <div className="fade-up">
      <PageHead
        title="Audit Log"
        description="Kim, ne zaman, hangi kapsamda hangi işlemi gerçekleştirdi."
      >
        <Badge tone="green">Değiştirilemez kayıt</Badge>
      </PageHead>
      <div className="grid gap-4 md:grid-cols-3">
        <StatCard
          label="Toplam kayıt"
          value={String(items.length)}
          detail="Aktif oturum süresince"
          icon={ScrollText}
        />
        <StatCard
          label="Başarılı giriş"
          value={String(
            items.filter((x) => x.action === "auth.login.success").length,
          )}
          detail="Kimlik doğrulama olayları"
          icon={ShieldCheck}
          tone="green"
        />
        <StatCard
          label="Yapılandırma değişikliği"
          value={String(
            items.filter((x) => x.action.includes("integration")).length,
          )}
          detail="Şifreli entegrasyon işlemleri"
          icon={Plug}
          tone="amber"
        />
      </div>
      <Card className="mt-5">
        <CardHeader>
          <CardTitle>Güvenlik olayları</CardTitle>
          <Badge tone="blue">En yeni önce</Badge>
        </CardHeader>
        <CardContent className="overflow-x-auto p-0">
          {loading ? (
            <div className="p-8 text-sm text-slate-500">
              Audit kayıtları yükleniyor…
            </div>
          ) : (
            <table className="data-table min-w-[850px]">
              <thead>
                <tr>
                  {[
                    "Zaman",
                    "Olay",
                    "Aktör / Hedef",
                    "Tenant",
                    "IP",
                    "Sonuç",
                  ].map((x) => (
                    <th key={x}>{x}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {items.map((item, index) => (
                  <tr key={item.at + index}>
                    <td className="text-xs text-slate-500">
                      {new Date(item.at).toLocaleString("tr-TR")}
                    </td>
                    <td>
                      <code className="rounded bg-slate-100 px-2 py-1 text-xs">
                        {item.action}
                      </code>
                    </td>
                    <td className="font-semibold">
                      {item.actor || item.subject}
                    </td>
                    <td className="text-xs text-slate-500">
                      {item.tenantId || "—"}
                    </td>
                    <td className="font-mono text-xs text-slate-400">
                      {item.ip || "—"}
                    </td>
                    <td>
                      <Badge tone="green">Başarılı</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

type PrivacyRequest = {
  id: string;
  employeeId: string;
  type: "export" | "delete";
  status: "pending" | "processing" | "completed" | "failed";
  requestedAt: string;
  receiptId?: string | null;
  failure?: string;
};

function PrivacyCenter({ user, csrf }: { user: SessionUser; csrf: string }) {
  const [items, setItems] = useState<PrivacyRequest[]>([]);
  const [busy, setBusy] = useState(false);
  const canManage = user.permissions.includes("users:manage");

  const load = () =>
    fetch("/api/v1/privacy/requests", { credentials: "same-origin" })
      .then((response) => response.ok ? response.json() : Promise.reject(new Error("Talepler yüklenemedi")))
      .then((result) => setItems(result.items || []))
      .catch(() => notify("Veri hakkı talepleri yüklenemedi", "error"));

  useEffect(() => { void load(); }, []);

  async function createRequest(type: "export" | "delete") {
    if (type === "delete" && !window.confirm("Silme talebi geri alınamaz bir işleme dönüşebilir. Talep oluşturulsun mu?")) return;
    setBusy(true);
    const response = await fetch("/api/v1/privacy/requests", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-CSRF-Token": csrf },
      body: JSON.stringify({ type }),
    });
    setBusy(false);
    if (!response.ok) return notify("Talep oluşturulamadı", "error");
    notify(type === "export" ? "Veri dışa aktarma talebi oluşturuldu" : "Veri silme talebi oluşturuldu");
    await load();
  }

  async function executeRequest(request: PrivacyRequest) {
    if (request.type === "delete" && !window.confirm(`${request.employeeId} için kalıcı silme işlemi başlatılsın mı?`)) return;
    setBusy(true);
    const response = await fetch(`/api/v1/privacy/requests/${encodeURIComponent(request.id)}/execute`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "X-CSRF-Token": csrf },
    });
    setBusy(false);
    if (!response.ok) return notify("PlayFab gizlilik işlemi tamamlanamadı", "error");
    notify("Gizlilik işlemi sağlayıcıya iletildi");
    await load();
  }

  return (
    <div className="fade-up">
      <PageHead
        title="Veri Hakları Merkezi"
        description="Eğitim verilerinin dışa aktarılması ve silinmesi için izlenebilir talep süreci."
      >
        <Button variant="outline" disabled={busy} onClick={() => createRequest("export")}>
          <Download size={16} /> Verilerimi iste
        </Button>
        <Button variant="outline" disabled={busy} onClick={() => createRequest("delete")}>
          <AlertTriangle size={16} /> Silme talebi
        </Button>
      </PageHead>
      <div className="grid gap-4 md:grid-cols-3">
        <StatCard label="Bekleyen" value={String(items.filter((item) => item.status === "pending").length)} detail="Yönetici değerlendirmesi" icon={Clock3} tone="amber" />
        <StatCard label="Tamamlanan" value={String(items.filter((item) => item.status === "completed").length)} detail="Makbuzlu sağlayıcı işlemi" icon={CheckCircle2} tone="green" />
        <StatCard label="Toplam talep" value={String(items.length)} detail={canManage ? "Tenant kapsamı" : "Kişisel kapsam"} icon={ClipboardCheck} />
      </div>
      <Card className="mt-5">
        <CardHeader>
          <CardTitle>Talep geçmişi</CardTitle>
          <Badge tone="blue">Şifreli kayıt</Badge>
        </CardHeader>
        <CardContent className="overflow-x-auto p-0">
          <table className="data-table min-w-[760px]">
            <thead><tr><th>Tarih</th><th>Çalışan</th><th>İşlem</th><th>Durum</th><th>Makbuz</th>{canManage && <th>Aksiyon</th>}</tr></thead>
            <tbody>
              {items.length === 0 ? (
                <tr><td colSpan={canManage ? 6 : 5} className="py-10 text-center text-slate-400">Henüz talep bulunmuyor.</td></tr>
              ) : items.map((request) => (
                <tr key={request.id}>
                  <td className="text-xs text-slate-500">{new Date(request.requestedAt).toLocaleString("tr-TR")}</td>
                  <td className="font-semibold">{request.employeeId}</td>
                  <td>{request.type === "export" ? "Dışa aktarım" : "Kalıcı silme"}</td>
                  <td><Badge tone={request.status === "completed" ? "green" : request.status === "failed" ? "red" : "amber"}>{request.status}</Badge></td>
                  <td className="font-mono text-xs">{request.receiptId || "—"}</td>
                  {canManage && <td>{request.status === "pending" ? <Button size="sm" disabled={busy} onClick={() => executeRequest(request)}>İşle</Button> : "—"}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </CardContent>
      </Card>
    </div>
  );
}

function IntegrationSettings({
  user,
  csrf,
}: {
  user: SessionUser;
  csrf: string;
}) {
  const [form, setForm] = useState({
      tenantId: user.role === "super_admin" ? "tenant-cedas" : user.tenantId,
      provider: "demo",
      titleId: "",
      dataUrl: "",
      serviceToken: "",
      serviceTokenMasked: "",
      hasServiceToken: false,
      updatedAt: "",
    }),
    [status, setStatus] = useState(""),
    [busy, setBusy] = useState(false);
  useEffect(() => {
    const query =
      user.role === "super_admin"
        ? `?tenantId=${encodeURIComponent(form.tenantId)}`
        : "";
    fetch("/api/v1/integrations/playfab" + query, {
      credentials: "same-origin",
    })
      .then((r) => r.json())
      .then((x) => setForm((f) => ({ ...f, ...x.settings })));
  }, []);
  async function save() {
    setBusy(true);
    setStatus("Bağlantı test ediliyor…");
    const r = await fetch("/api/v1/integrations/playfab", {
      method: "PUT",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-CSRF-Token": csrf },
      body: JSON.stringify(form),
    });
    const result = await r.json();
    if (r.ok) {
      setForm((f) => ({ ...f, ...result.settings, serviceToken: "" }));
      setStatus(
        "Ayarlar şifreli olarak kaydedildi ve veri sağlayıcısı aktif edildi.",
      );
    } else setStatus(result.message || result.error || "Ayarlar kaydedilemedi");
    setBusy(false);
  }
  return (
    <div className="fade-up">
      <PageHead
        title="Entegrasyon Ayarları"
        description="PlayFab hesabını panelden güvenli şekilde yönetin."
      >
        <Badge tone={form.provider === "playfab" ? "green" : "blue"}>
          {form.provider === "playfab" ? "PlayFab aktif" : "Demo aktif"}
        </Badge>
      </PageHead>
      <div className="grid gap-5 xl:grid-cols-[1.25fr_.75fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Veri sağlayıcısı</CardTitle>
              <p className="mt-1 text-xs text-slate-500">
                Gizli değerler AES-256-GCM ile saklanır; tarayıcıya geri
                gönderilmez.
              </p>
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            <label className="block">
              <span className="mb-2 block text-sm font-semibold">
                Sağlayıcı
              </span>
              <select
                value={form.provider}
                onChange={(e) => setForm({ ...form, provider: e.target.value })}
                className="compact-control"
              >
                <option value="demo">Demo veri</option>
                <option value="playfab">PlayFab / Analytics Gateway</option>
              </select>
            </label>
            <div className="grid gap-4 md:grid-cols-2">
              <label>
                <span className="mb-2 block text-sm font-semibold">
                  PlayFab Title ID
                </span>
                <input
                  value={form.titleId}
                  onChange={(e) =>
                    setForm({ ...form, titleId: e.target.value })
                  }
                  placeholder="ABCDE"
                  className="compact-control"
                />
              </label>
              <label>
                <span className="mb-2 block text-sm font-semibold">Tenant</span>
                <input
                  disabled={user.role !== "super_admin"}
                  value={form.tenantId}
                  onChange={(e) =>
                    setForm({ ...form, tenantId: e.target.value })
                  }
                  className="compact-control"
                />
              </label>
            </div>
            <label className="block">
              <span className="mb-2 block text-sm font-semibold">
                Analytics Gateway URL
              </span>
              <input
                value={form.dataUrl}
                onChange={(e) => setForm({ ...form, dataUrl: e.target.value })}
                placeholder="https://analytics.example.com/"
                className="compact-control"
              />
            </label>
            <label className="block">
              <span className="mb-2 flex justify-between text-sm font-semibold">
                Servis Tokenı{" "}
                {form.hasServiceToken && (
                  <span className="font-normal text-emerald-600">
                    Kayıtlı: {form.serviceTokenMasked}
                  </span>
                )}
              </span>
              <input
                type="password"
                value={form.serviceToken}
                onChange={(e) =>
                  setForm({ ...form, serviceToken: e.target.value })
                }
                placeholder={
                  form.hasServiceToken
                    ? "Değiştirmek için yeni token girin"
                    : "Güvenli servis tokenı"
                }
                autoComplete="new-password"
                className="compact-control"
              />
            </label>
            {status && (
              <div
                role="status"
                className={cn(
                  "rounded-xl p-4 text-sm",
                  status.includes("kaydedildi")
                    ? "bg-emerald-50 text-emerald-800"
                    : "bg-blue-50 text-blue-800",
                )}
              >
                {status}
              </div>
            )}
            <Button onClick={save} disabled={busy}>
              <Save size={16} />
              {busy ? "Test ediliyor…" : "Bağlantıyı test et ve kaydet"}
            </Button>
          </CardContent>
        </Card>
        <div className="space-y-4">
          <Card>
            <CardContent className="p-5">
              <div className="grid size-10 place-items-center rounded-xl bg-emerald-50 text-emerald-600">
                <ShieldCheck size={19} />
              </div>
              <h3 className="mt-4 font-bold">Güvenlik garantileri</h3>
              <ul className="mt-3 space-y-2 text-xs leading-5 text-slate-500">
                <li>• Token hiçbir API yanıtında düz metin dönmez.</li>
                <li>• Bağlantı testi başarısızsa sağlayıcı değişmez.</li>
                <li>
                  • Her değişiklik aktör ve tenant ile audit log’a yazılır.
                </li>
                <li>• Admin yalnız kendi tenant ayarını değiştirebilir.</li>
              </ul>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-5">
              <span className="text-xs font-semibold text-slate-400">
                SON GÜNCELLEME
              </span>
              <p className="mt-2 text-sm font-semibold">
                {form.updatedAt
                  ? new Date(form.updatedAt).toLocaleString("tr-TR")
                  : "Henüz yapılandırılmadı"}
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function SettingsPage() {
  return (
    <div className="fade-up">
      <PageHead
        title="Ayarlar"
        description="Portal davranışı, veri bağlantısı ve bildirim tercihleri."
      />
      <div className="grid gap-4 lg:grid-cols-2">
        {[
          [
            "Veri bağlantısı",
            "Demo sağlayıcısı aktif",
            "PlayFab bağlantısı deployment secret’ları üzerinden değiştirilebilir.",
          ],
          [
            "Bildirimler",
            "Haftalık özet açık",
            "Risk eşiği aşıldığında yöneticilere bildirim gönderilir.",
          ],
          [
            "Erişim kontrolü",
            "Rol tabanlı erişim",
            "Yönetici ve çalışan yetkileri sunucu katmanında ayrıştırılır.",
          ],
          [
            "Veri saklama",
            "Politika bekliyor",
            "Kurum saklama süresi belirlenmeden üretim verisi otomatik silinmez.",
          ],
        ].map(([a, b, c], i) => (
          <Card key={a}>
            <CardContent className="flex items-start gap-4 p-5">
              <div className="grid size-10 place-items-center rounded-xl bg-slate-100 text-slate-600">
                {i === 0 ? (
                  <Activity size={19} />
                ) : i === 1 ? (
                  <Bell size={19} />
                ) : i === 2 ? (
                  <ShieldCheck size={19} />
                ) : (
                  <Clock3 size={19} />
                )}
              </div>
              <div className="flex-1">
                <h3 className="text-sm font-bold">{a}</h3>
                <p className="mt-1 text-xs leading-5 text-slate-500">{c}</p>
              </div>
              <Badge tone={i === 0 ? "blue" : "green"}>{b}</Badge>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

function Shell({
  data,
  user,
  csrf,
  onLogout,
}: {
  data: Bootstrap;
  user: SessionUser;
  csrf: string;
  onLogout: () => void;
}) {
  const [page, setPage] = useState<Page>("dashboard"),
    [mobile, setMobile] = useState(false),
    [globalQuery, setGlobalQuery] = useState(""),
    [notificationsOpen, setNotificationsOpen] = useState(false),
    [profileOpen, setProfileOpen] = useState(false);
  const allowedPages = nav
    .filter(([, , , permission]) => hasPermission(user, permission))
    .map(([id]) => id);
  useEffect(() => {
    const handler = (event: Event) => {
      const next = (event as CustomEvent<Page>).detail;
      if (!allowedPages.includes(next)) {
        notify("Bu ekran için yetkiniz bulunmuyor", "error");
        return;
      }
      setPage(next);
      history.replaceState(null, "", `#/${next}`);
    };
    window.addEventListener("cedas:navigate", handler);
    return () => window.removeEventListener("cedas:navigate", handler);
  }, [user.role]);
  const searchResults =
    globalQuery.length > 1
      ? [
          ...data.employees
            .filter((e) =>
              (e.name + e.id).toLowerCase().includes(globalQuery.toLowerCase()),
            )
            .slice(0, 5)
            .map((e) => ({
              label: e.name,
              meta: e.id,
              page: "employees" as Page,
            })),
          ...data.content.levels
            .filter((l) =>
              l.name.toLowerCase().includes(globalQuery.toLowerCase()),
            )
            .slice(0, 3)
            .map((l) => ({
              label: l.name,
              meta: "Senaryo",
              page: "scenarios" as Page,
            })),
        ]
      : [];
  const content =
    page === "dashboard" ? (
      <Dashboard data={data} />
    ) : page === "employees" ? (
      <Employees data={data} />
    ) : page === "scenarios" ? (
      <Scenarios data={data} />
    ) : page === "risks" ? (
      <Risks data={data} />
    ) : page === "reports" ? (
      <Reports data={data} />
    ) : page === "access" ? (
      <AccessManagement user={user} data={data} />
    ) : page === "audit" ? (
      <AuditLogs />
    ) : page === "privacy" ? (
      <PrivacyCenter user={user} csrf={csrf} />
    ) : page === "integrations" ? (
      <IntegrationSettings user={user} csrf={csrf} />
    ) : (
      <SettingsPage />
    );
  return (
    <div className="min-h-dvh bg-slate-50">
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-slate-200 bg-white p-4 transition-transform lg:translate-x-0",
          mobile ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <div className="flex h-14 items-center justify-between px-2">
          <Logo />
          <button className="lg:hidden" onClick={() => setMobile(false)}>
            <X size={20} />
          </button>
        </div>
        <nav className="mt-7 space-y-1">
          {nav
            .filter(([, , , permission]) => hasPermission(user, permission))
            .map(([id, label, Icon]) => (
              <button
                key={id}
                onClick={() => {
                  setPage(id);
                  history.replaceState(null, "", `#/${id}`);
                  setMobile(false);
                }}
                className={cn(
                  "flex h-11 w-full items-center gap-3 rounded-xl px-3 text-sm font-medium transition",
                  page === id
                    ? "bg-blue-50 text-blue-700"
                    : "text-slate-500 hover:bg-slate-50 hover:text-slate-950",
                )}
              >
                <Icon size={18} />
                {label}
                {id === "risks" && (
                  <Badge tone="red" className="ml-auto">
                    4
                  </Badge>
                )}
              </button>
            ))}
        </nav>
        <div className={cn("mt-auto rounded-2xl border p-4", data.IS_MOCK ? "border-amber-200 bg-amber-50" : "border-emerald-200 bg-emerald-50")}>
          <div className={cn("flex items-center gap-2 text-xs font-bold", data.IS_MOCK ? "text-amber-800" : "text-emerald-800")}>
            <Sparkles size={15} />
            {data.IS_MOCK ? "Demo sağlayıcısı" : `${data.PROVIDER || "PlayFab"} canlı`}
          </div>
          <p className={cn("mt-2 text-[11px] leading-5", data.IS_MOCK ? "text-amber-700" : "text-emerald-700")}>
            {data.IS_MOCK
              ? "Bu kayıtlar yapaydır ve operasyonel karar için kullanılamaz."
              : "Olaylar doğrulanmış telemetri API sözleşmesinden alınır."}
          </p>
        </div>
        <button
          onClick={onLogout}
          className="mt-3 flex h-11 items-center gap-3 rounded-xl px-3 text-sm font-medium text-slate-500 hover:bg-red-50 hover:text-red-600"
        >
          <LogOut size={18} />
          Oturumu kapat
        </button>
      </aside>
      {mobile && (
        <button
          aria-label="Menüyü kapat"
          className="fixed inset-0 z-30 bg-slate-950/20 lg:hidden"
          onClick={() => setMobile(false)}
        />
      )}
      <div className="lg:pl-64">
        <header className="sticky top-0 z-20 flex h-[72px] items-center gap-4 border-b border-slate-200/80 bg-white/90 px-4 backdrop-blur-xl sm:px-7">
          <button
            className="grid size-10 place-items-center rounded-lg border lg:hidden"
            onClick={() => setMobile(true)}
          >
            <Menu size={19} />
          </button>
          <div className="relative hidden max-w-sm flex-1 md:block">
            <Search
              className="absolute left-3 top-2.5 text-slate-400"
              size={17}
            />
            <input
              value={globalQuery}
              onChange={(e) => setGlobalQuery(e.target.value)}
              className="h-10 w-full rounded-xl border border-slate-200 bg-slate-50 pl-10 pr-3 text-sm outline-none focus:bg-white focus:ring-4 focus:ring-blue-50"
              placeholder="Çalışan, senaryo veya rapor ara..."
            />
            {searchResults.length > 0 && (
              <div className="absolute left-0 right-0 top-12 z-50 rounded-xl border bg-white p-2 shadow-xl">
                {searchResults.map((result) => (
                  <button
                    key={result.label + result.meta}
                    onClick={() => {
                      navigateTo(result.page);
                      setGlobalQuery("");
                      notify(`${result.label} bulundu`, "info");
                    }}
                    className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-left hover:bg-slate-50"
                  >
                    <span className="text-sm font-semibold">
                      {result.label}
                    </span>
                    <span className="text-xs text-slate-400">
                      {result.meta}
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>
          <div className="ml-auto flex items-center gap-2">
            <Badge tone="green">
              <span className="mr-1.5 size-1.5 rounded-full bg-emerald-500" />
              Sistem aktif
            </Badge>
            <div className="relative">
              <button
                aria-label="Bildirimleri aç"
                onClick={() => {
                  setNotificationsOpen((value) => !value);
                  setProfileOpen(false);
                }}
                className="relative grid size-10 place-items-center rounded-xl text-slate-500 hover:bg-slate-100"
              >
                <Bell size={19} />
                <span className="absolute right-2 top-2 size-2 rounded-full border-2 border-white bg-red-500" />
              </button>
              {notificationsOpen && (
                <div className="absolute right-0 top-12 z-50 w-80 rounded-xl border bg-white p-3 shadow-xl">
                  <div className="mb-2 flex justify-between">
                    <b className="text-sm">Bildirimler</b>
                    <button
                      onClick={() => {
                        setNotificationsOpen(false);
                        notify("Bildirimler okundu");
                      }}
                      className="text-xs font-semibold text-blue-600"
                    >
                      Tümünü okundu say
                    </button>
                  </div>
                  {[
                    ["Risk eşiği aşıldı", "EMP-1045 takip gerektiriyor"],
                    ["Haftalık rapor hazır", "Yönetim özeti oluşturuldu"],
                  ].map(([a, b]) => (
                    <div key={a} className="border-t py-3">
                      <b className="text-xs">{a}</b>
                      <p className="mt-1 text-xs text-slate-500">{b}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
            <div className="mx-1 h-7 w-px bg-slate-200" />
            <div className="relative">
              <button
                onClick={() => {
                  setProfileOpen((value) => !value);
                  setNotificationsOpen(false);
                }}
                className="flex items-center gap-3 rounded-xl p-1.5 hover:bg-slate-50"
              >
                <div className="grid size-9 place-items-center rounded-full bg-slate-900 text-xs font-bold text-white">
                  {user.name
                    .split(" ")
                    .map((x) => x[0])
                    .join("")
                    .slice(0, 2)}
                </div>
                <div className="hidden text-left sm:block">
                  <b className="block text-xs">{user.name}</b>
                  <span className="text-[10px] text-slate-400">
                    {user.role}
                  </span>
                </div>
                <ChevronDown size={15} className="text-slate-400" />
              </button>
              {profileOpen && (
                <div className="absolute right-0 top-12 z-50 w-56 rounded-xl border bg-white p-2 shadow-xl">
                  {hasPermission(user, "users:manage") && (
                    <button
                      onClick={() => {
                        navigateTo("access");
                        setProfileOpen(false);
                      }}
                      className="w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-slate-50"
                    >
                      Yetkilerimi görüntüle
                    </button>
                  )}
                  <button
                    onClick={onLogout}
                    className="w-full rounded-lg px-3 py-2 text-left text-sm text-red-600 hover:bg-red-50"
                  >
                    Oturumu kapat
                  </button>
                </div>
              )}
            </div>
          </div>
        </header>
        <main className="mx-auto max-w-[1600px] p-4 sm:p-7">{content}</main>
        <ToastHost />
      </div>
    </div>
  );
}

export default function App() {
  const [data, setData] = useState<Bootstrap | null>(null),
    [user, setUser] = useState<SessionUser | null>(null),
    [csrf, setCsrf] = useState(""),
    [checking, setChecking] = useState(true),
    [runtime, setRuntime] = useState({ provider: "unknown", demo: false }),
    [error, setError] = useState("");
  const loadData = () =>
    fetch("/api/v1/bootstrap", { credentials: "same-origin" })
      .then((r) => {
        if (!r.ok) throw new Error("Veri kapsamına erişilemedi");
        return r.json();
      })
      .then(setData);
  useEffect(() => {
    Promise.all([
      fetch("/api/v1/runtime", { credentials: "same-origin" })
        .then((response) => response.ok ? response.json() : null)
        .catch(() => null),
      fetch("/api/v1/auth/me", { credentials: "same-origin" }),
    ])
      .then(async ([runtimeInfo, authResponse]) => {
        if (runtimeInfo) setRuntime(runtimeInfo);
        if (!authResponse.ok) return;
        const session = await authResponse.json();
        setUser(session.user);
        setCsrf(session.csrfToken);
        await loadData();
      })
      .catch(() => {})
      .finally(() => setChecking(false));
  }, []);
  async function login(id: string, password: string, remember: boolean) {
    setError("");
    const r = await fetch("/api/v1/auth/login", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ id, password, remember }),
    });
    if (!r.ok) {
      setError("Giriş başarısız. Bilgileri kontrol edin.");
      return;
    }
    const session = await r.json();
    setUser(session.user);
    setCsrf(session.csrfToken);
    await loadData();
  }
  async function logout() {
    await fetch("/api/v1/auth/logout", {
      method: "POST",
      credentials: "same-origin",
      headers: { "X-CSRF-Token": csrf },
    });
    setUser(null);
    setData(null);
    setCsrf("");
  }
  if (checking)
    return (
      <div className="grid h-dvh place-items-center bg-white">
        <div className="text-center">
          <div className="mx-auto size-10 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
          <p className="mt-4 text-sm text-slate-500">
            Güvenli oturum doğrulanıyor...
          </p>
        </div>
      </div>
    );
  if (!user)
    return (
      <>
        <Login onLogin={login} isDemo={runtime.demo} />
        {error && (
          <div
            role="alert"
            className="fixed bottom-5 left-1/2 z-50 -translate-x-1/2 rounded-xl bg-red-600 px-4 py-3 text-sm font-semibold text-white shadow-xl"
          >
            {error}
          </div>
        )}
      </>
    );
  if (!data)
    return (
      <div className="grid h-dvh place-items-center bg-white">
        <div className="text-center">
          <div className="mx-auto size-10 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
          <p className="mt-4 text-sm text-slate-500">
            Yetkili veri kapsamı hazırlanıyor...
          </p>
        </div>
      </div>
    );
  return <Shell data={data} user={user} csrf={csrf} onLogout={logout} />;
}
