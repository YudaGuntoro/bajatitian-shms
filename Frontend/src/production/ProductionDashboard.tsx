"use client";

import { ApexOptions } from "apexcharts";
import dynamic from "next/dynamic";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTheme } from "@/context/ThemeContext";
import { useToast } from "@/context/ToastContext";
import { apiGet } from "@/lib/api";
import { readBridgeLocationSettings } from "./locationSettings";
import { readMqttConfiguration, type MqttSensorCode } from "./mqttConfiguration";

const ReactApexChart = dynamic(() => import("react-apexcharts"), { ssr: false });

const SENSOR_ONLINE_WINDOW_MS = 60_000;
const sensorOrder: MqttSensorCode[] = ["TILT", "VW", "ATRH", "ACC"];

type HealthState = "online" | "offline" | "unknown";

type SHMSStatus = {
  last_mqtt_at?: string | null;
  main_server?: MainServerStatus;
};

type MainServerStatus = {
  configured: boolean;
  server_name: string;
  endpoint_url?: string | null;
  online: boolean;
  outage_started_at?: string | null;
  downtime_seconds: number;
  last_success_at?: string | null;
  last_failure_at?: string | null;
  last_error?: string | null;
  redis_buffer_count: number;
  db_spillover_count: number;
};

type MqttBrokerStatus = {
  configured: boolean;
  host?: string;
  online: boolean;
  port?: number;
};

type LogBuffer = {
  id: number;
  device_id: string;
  payload: string;
  status?: string;
  time_stamp: string;
  uploaded_at?: string | null;
};

type PagedLogBuffer = {
  data: LogBuffer[];
  page_number: number;
  page_size: number;
  total_records: number;
  total_pages: number;
};

type DashboardState = {
  broker: MqttBrokerStatus | null;
  lastMqttAt: string | null;
  logs: LogBuffer[];
  mainServer: MainServerStatus | null;
  totalBuffered: number;
};

const timeFormatter = new Intl.DateTimeFormat("en-GB", {
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false,
});

function parseDate(value?: string | null) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatTime(value?: string | null) {
  const date = parseDate(value);
  return date ? timeFormatter.format(date) : "--:--:--";
}

function formatRelative(value?: string | null, now = 0) {
  const date = parseDate(value);
  if (!date) return "No data";

  const diffSeconds = Math.max(0, Math.floor((now - date.getTime()) / 1000));
  if (diffSeconds < 60) return `${diffSeconds}s ago`;

  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  return `${Math.floor(diffMinutes / 60)}h ago`;
}

function formatDuration(totalSeconds: number) {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  if (seconds < 60) return `${seconds}s`;

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;

  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

function isToday(value: string, now: number) {
  const date = parseDate(value);
  if (!date) return false;

  const current = new Date(now);
  return (
    date.getFullYear() === current.getFullYear() &&
    date.getMonth() === current.getMonth() &&
    date.getDate() === current.getDate()
  );
}

function isRecent(value: string | null, now: number) {
  const date = parseDate(value);
  return date ? now - date.getTime() <= SENSOR_ONLINE_WINDOW_MS : false;
}

function getLogSensor(log: LogBuffer): MqttSensorCode | null {
  const source = `${log.device_id} ${log.payload}`.toUpperCase();
  return sensorOrder.find((code) => source.includes(code)) ?? null;
}

function statusClasses(status: HealthState) {
  if (status === "online") {
    return "bg-teal-50 text-teal-700 ring-1 ring-teal-600/15 dark:bg-teal-500/10 dark:text-teal-200 dark:ring-teal-400/20";
  }

  if (status === "offline") {
    return "bg-red-50 text-red-700 ring-1 ring-red-600/15 dark:bg-red-500/10 dark:text-red-200 dark:ring-red-400/20";
  }

  return "bg-slate-100 text-slate-600 ring-1 ring-slate-500/15 dark:bg-slate-800 dark:text-slate-300 dark:ring-slate-500/25";
}

function StatusPill({ label, status }: { label: string; status: HealthState }) {
  return (
    <span className={`inline-flex w-fit items-center gap-2 rounded-full px-3 py-1 text-xs font-black ${statusClasses(status)}`}>
      <span className={`size-2 rounded-full ${status === "online" ? "bg-teal-600" : status === "offline" ? "bg-red-600" : "bg-slate-400"}`} />
      {label}
    </span>
  );
}

function MetricCard({
  accent,
  label,
  note,
  status,
  value,
}: {
  accent: string;
  label: string;
  note: string;
  status?: HealthState;
  value: React.ReactNode;
}) {
  return (
    <div className="relative overflow-hidden rounded-lg border border-slate-200 bg-white p-5 shadow-sm shadow-slate-200/50 dark:border-slate-800 dark:bg-slate-900 dark:shadow-black/10">
      <span className={`absolute bottom-0 left-0 top-0 w-1 ${accent}`} />
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-semibold text-slate-500 dark:text-slate-400">{label}</p>
        {status ? <StatusPill label={status === "online" ? "Online" : status === "offline" ? "Offline" : "Waiting"} status={status} /> : null}
      </div>
      <div className="mt-3 text-2xl font-black tracking-tight text-slate-900 dark:text-white">{value}</div>
      <p className="mt-2 text-xs font-semibold text-slate-400">{note}</p>
    </div>
  );
}

export default function ProductionDashboard() {
  const { theme } = useTheme();
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [now, setNow] = useState(() => Date.now());
  const [configuration] = useState(() => readMqttConfiguration());
  const [locationSettings] = useState(() => readBridgeLocationSettings());
  const [dashboard, setDashboard] = useState<DashboardState>({
    broker: null,
    lastMqttAt: null,
    logs: [],
    mainServer: null,
    totalBuffered: 0,
  });

  const load = useCallback(async () => {
    setLoading(true);
    const loadTime = Date.now();

    try {
      const [statusResult, brokerResult, bufferResult] = await Promise.allSettled([
        apiGet<SHMSStatus>("/api/shms-system/status"),
        apiGet<MqttBrokerStatus>("/api/shms-system/mqtt-broker/status"),
        apiGet<PagedLogBuffer>("/api/log-buffer?page=1&limit=50"),
      ]);

      const mainServer = statusResult.status === "fulfilled" ? statusResult.value.main_server ?? null : null;

      setNow(loadTime);
      setDashboard({
        broker: brokerResult.status === "fulfilled" ? brokerResult.value : null,
        lastMqttAt: statusResult.status === "fulfilled" ? statusResult.value.last_mqtt_at ?? null : null,
        logs: bufferResult.status === "fulfilled" ? bufferResult.value.data : [],
        mainServer,
        totalBuffered: mainServer?.redis_buffer_count ?? 0,
      });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load dashboard." });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), 10_000);
    return () => window.clearInterval(timer);
  }, [load]);

  const brokerStatus: HealthState = dashboard.broker?.online ? "online" : "offline";
  const mainServerStatus: HealthState = dashboard.mainServer
    ? dashboard.mainServer.online ? "online" : "offline"
    : "unknown";
  const sensorInputStatus: HealthState = isRecent(dashboard.lastMqttAt, now) ? "online" : "offline";
  const sensorRows = useMemo(() => {
    return sensorOrder.map((code) => {
      const config = configuration.topics.find((topic) => topic.code === code);
      const logs = dashboard.logs.filter((log) => getLogSensor(log) === code);
      const lastAt = logs[0]?.time_stamp ?? null;
      const active = config?.enabled ?? false;

      return {
        active,
        code,
        lastAt,
        name: config?.name ?? code,
        online: active && isRecent(lastAt, now),
        today: logs.filter((log) => isToday(log.time_stamp, now)).length,
        topic: config?.topic ?? "-",
      };
    });
  }, [configuration.topics, dashboard.logs, now]);

  const recentActivities = useMemo(() => {
    return dashboard.logs.slice(0, 12).map((log) => {
      const sensor = getLogSensor(log);
      return {
        id: log.id,
        label: sensor ? `${sensor} message received` : `${log.device_id} message received`,
        payload: log.payload,
        time: log.time_stamp,
      };
    });
  }, [dashboard.logs]);

  const chartData = useMemo(() => {
    const buckets = Array.from({ length: 12 }, (_, index) => {
      const date = new Date(now);
      date.setHours(date.getHours() - (11 - index), 0, 0, 0);
      return {
        end: date.getTime() + 60 * 60 * 1000,
        label: `${date.getHours().toString().padStart(2, "0")}:00`,
        start: date.getTime(),
      };
    });

    return {
      categories: buckets.map((bucket) => bucket.label),
      incoming: buckets.map((bucket) => dashboard.logs.filter((log) => {
        const time = parseDate(log.time_stamp)?.getTime() ?? 0;
        return time >= bucket.start && time < bucket.end;
      }).length),
      sent: buckets.map((bucket) => dashboard.logs.filter((log) => {
        const time = parseDate(log.uploaded_at ?? log.time_stamp)?.getTime() ?? 0;
        return log.status === "uploaded" && time >= bucket.start && time < bucket.end;
      }).length),
    };
  }, [dashboard.logs, now]);

  const chartOptions = useMemo<ApexOptions>(() => ({
    chart: {
      fontFamily: "Outfit, sans-serif",
      toolbar: { show: false },
      type: "area",
    },
    colors: ["#2563eb", "#0f766e"],
    dataLabels: { enabled: false },
    fill: {
      opacity: 0.18,
      type: "solid",
    },
    grid: {
      borderColor: theme === "dark" ? "#1e293b" : "#e2e8f0",
      strokeDashArray: 3,
    },
    legend: {
      fontFamily: "Outfit",
      horizontalAlign: "left",
      position: "top",
    },
    stroke: {
      curve: "smooth",
      width: 3,
    },
    tooltip: {
      y: {
        formatter: (value: number) => `${value} message`,
      },
    },
    xaxis: {
      axisBorder: { show: false },
      axisTicks: { show: false },
      categories: chartData.categories,
      labels: {
        style: {
          colors: theme === "dark" ? "#cbd5e1" : "#475569",
          fontFamily: "Outfit, sans-serif",
        },
      },
    },
    yaxis: {
      decimalsInFloat: 0,
      labels: {
        formatter: (value: number) => `${Math.round(value)}`,
        style: {
          colors: theme === "dark" ? "#cbd5e1" : "#475569",
          fontFamily: "Outfit, sans-serif",
        },
      },
      min: 0,
    },
  }), [chartData.categories, theme]);

  const chartSeries = useMemo(() => [
    { data: chartData.incoming, name: "Data In" },
    { data: chartData.sent, name: "Uploaded" },
  ], [chartData.incoming, chartData.sent]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-brand-600">Middleware</p>
          <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Dashboard</h1>
        </div>
        <button
          className="h-10 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white transition hover:bg-brand-600 disabled:opacity-60"
          disabled={loading}
          onClick={() => void load()}
          type="button"
        >
          {loading ? "Refreshing" : "Refresh"}
        </button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          accent="bg-blue-600"
          label="MQTT Broker"
          note={dashboard.broker ? `${dashboard.broker.host ?? "localhost"}:${dashboard.broker.port ?? 1883}` : "Checking local broker"}
          status={brokerStatus}
          value={dashboard.broker?.online ? "Ready" : "Unavailable"}
        />
        <MetricCard
          accent="bg-slate-500"
          label={dashboard.mainServer?.server_name ?? "Witon Server"}
          note={dashboard.mainServer?.endpoint_url || "Endpoint upload belum dikonfigurasi"}
          status={mainServerStatus}
          value={mainServerStatus === "online" ? "Reachable" : mainServerStatus === "offline" ? "Unreachable" : "Not Set"}
        />
        <MetricCard
          accent="bg-teal-600"
          label="Last MQTT Received"
          note={formatRelative(dashboard.lastMqttAt, now)}
          status={sensorInputStatus}
          value={formatTime(dashboard.lastMqttAt)}
        />
        <MetricCard
          accent="bg-amber-500"
          label="Downtime"
          note={
            dashboard.mainServer?.outage_started_at
              ? `Since ${formatTime(dashboard.mainServer.outage_started_at)}`
              : "No active outage"
          }
          status={mainServerStatus}
          value={formatDuration(dashboard.mainServer?.downtime_seconds ?? 0)}
        />
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm shadow-slate-200/50 dark:border-slate-800 dark:bg-slate-900 dark:shadow-black/10">
        <div>
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Location Overview</p>
            <h2 className="mt-2 text-lg font-black text-slate-900 dark:text-white">
              {locationSettings.bridgeName || "Bridge name not set"}
            </h2>
          </div>
        </div>
        <div className="mt-5 grid gap-4 md:grid-cols-3">
          <div className="rounded-lg border border-slate-100 bg-slate-50 px-4 py-3 dark:border-slate-800 dark:bg-slate-950">
            <p className="text-xs font-bold uppercase text-slate-400">Area</p>
            <p className="mt-2 text-sm font-bold text-slate-800 dark:text-slate-100">{locationSettings.area || "-"}</p>
          </div>
          <div className="rounded-lg border border-slate-100 bg-slate-50 px-4 py-3 dark:border-slate-800 dark:bg-slate-950">
            <p className="text-xs font-bold uppercase text-slate-400">Bridge Name</p>
            <p className="mt-2 text-sm font-bold text-slate-800 dark:text-slate-100">{locationSettings.bridgeName || "-"}</p>
          </div>
          <div className="rounded-lg border border-slate-100 bg-slate-50 px-4 py-3 dark:border-slate-800 dark:bg-slate-950">
            <p className="text-xs font-bold uppercase text-slate-400">Map Location</p>
            <p className="mt-2 truncate text-sm font-bold text-slate-800 dark:text-slate-100">{locationSettings.mapQuery || "-"}</p>
          </div>
        </div>
      </section>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {sensorRows.map((sensor) => (
          <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900" key={sensor.code}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xl font-black text-slate-900 dark:text-white">{sensor.code}</p>
                <p className="mt-1 text-xs font-semibold text-slate-500 dark:text-slate-400">{sensor.name}</p>
              </div>
              <StatusPill
                label={!sensor.active ? "Inactive" : sensor.online ? "Online" : "Offline"}
                status={!sensor.active ? "unknown" : sensor.online ? "online" : "offline"}
              />
            </div>
            <div className="mt-5 space-y-3 text-sm">
              <div>
                <p className="text-xs font-bold uppercase text-slate-400">Topic</p>
                <p className="mt-1 truncate font-bold text-slate-700 dark:text-slate-200">{sensor.topic}</p>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <p className="text-xs font-bold uppercase text-slate-400">Last Received</p>
                  <p className="mt-1 font-bold text-slate-700 dark:text-slate-200">{formatRelative(sensor.lastAt, now)}</p>
                </div>
                <div>
                  <p className="text-xs font-bold uppercase text-slate-400">Today</p>
                  <p className="mt-1 font-bold text-slate-700 dark:text-slate-200">{sensor.today} msg</p>
                </div>
              </div>
            </div>
          </section>
        ))}
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_430px]">
        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div>
            <h2 className="text-lg font-bold text-slate-900 dark:text-white">Upload Health</h2>
            <p className="mt-1 text-xs font-semibold text-slate-400">Data masuk vs data terkirim per jam.</p>
          </div>
          <div className="mt-5 max-w-full overflow-x-auto">
            <div className="min-w-[640px]">
              <ReactApexChart height={290} options={chartOptions} series={chartSeries} type="area" />
            </div>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-bold text-slate-900 dark:text-white">Log Buffer Summary</h2>
          <div className="mt-5 grid grid-cols-2 gap-3">
            <MetricCard accent="bg-amber-500" label="Redis Buffer" note="Data menunggu upload" value={dashboard.totalBuffered} />
            <MetricCard accent="bg-red-600" label="DB Fallback" note="Redis dipindah ke DB" value={dashboard.mainServer?.db_spillover_count ?? 0} />
            <MetricCard accent="bg-teal-600" label="Last Upload Success" note={formatRelative(dashboard.mainServer?.last_success_at, now)} value={formatTime(dashboard.mainServer?.last_success_at)} />
            <MetricCard accent="bg-slate-500" label="Last Upload Failed" note={dashboard.mainServer?.last_error || "No error"} value={formatTime(dashboard.mainServer?.last_failure_at)} />
          </div>
          <div className="mt-4 rounded-lg border border-slate-100 p-4 dark:border-slate-800">
            <p className="text-xs font-bold uppercase text-slate-400">Oldest Pending Data</p>
            <p className="mt-2 text-sm font-bold text-slate-700 dark:text-slate-200">
              {dashboard.logs.length ? formatRelative(dashboard.logs[dashboard.logs.length - 1].time_stamp, now) : "No pending data"}
            </p>
          </div>
        </section>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <div>
            <h2 className="text-lg font-bold text-slate-900 dark:text-white">Recent Activity</h2>
            <p className="mt-1 text-xs font-semibold text-slate-400">Latest device messages captured by middleware.</p>
          </div>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[820px] border-separate border-spacing-0 text-left text-sm">
            <thead className="bg-transparent text-xs uppercase text-white">
              <tr>
                <th className="w-48 rounded-l-lg bg-brand-500 px-5 py-3">Time</th>
                <th className="w-56 bg-brand-500 px-4 py-3">Activity</th>
                <th className="rounded-r-lg bg-brand-500 px-4 py-3">Payload</th>
              </tr>
            </thead>
            <tbody>
              {recentActivities.map((activity) => (
                <tr key={activity.id}>
                  <td className="border-b border-slate-100 px-5 py-4 font-bold text-slate-700 dark:border-slate-800 dark:text-slate-200">
                    {formatTime(activity.time)}
                  </td>
                  <td className="border-b border-slate-100 px-4 py-4 font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">
                    {activity.label}
                  </td>
                  <td className="border-b border-slate-100 px-4 py-4 text-slate-500 dark:border-slate-800 dark:text-slate-400">
                    <span className="line-clamp-1 break-all">{activity.payload}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!recentActivities.length ? (
            <p className="py-10 text-center text-sm font-semibold text-slate-400">No device activity yet.</p>
          ) : null}
        </div>
      </section>
    </div>
  );
}
