"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { apiGet, apiRequest } from "@/lib/api";
import { useToast } from "@/context/ToastContext";
import { ConfirmModal } from "@/components/ui/modal/ConfirmModal";

type Policy = {
  enabled: boolean;
  reading_retention_days: number;
  uploaded_log_retention_days: number;
  cleanup_interval_minutes: number;
  max_reading_records: number | null;
};
type Preview = { readings_to_delete: number; logs_to_delete: number; protected_readings: number };
type Status = {
  cleanup_enabled: boolean; cleanup_pending: boolean;
  total_readings: number; total_logs: number; pending_logs: number;
  oldest_reading_at: string | null; last_cleanup_at: string | null;
  next_cleanup_at: string | null; deleted_readings: number; deleted_logs: number;
  last_error: string | null; capacity_exceeded: boolean;
};
const base = "/api/shms-system/storage";
const inputClass = "mt-2 h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-brand-500 focus:outline-none dark:border-slate-700 dark:bg-slate-950 dark:text-white";
const cardClass = "rounded-lg border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900";
const count = (value: number) => value.toLocaleString("en-GB");
const date = (value: string | null) => value ? new Date(value).toLocaleString("en-GB") : "None yet";

export default function StorageSettingsPage() {
  const toast = useToast();
  const [policy, setPolicy] = useState<Policy | null>(null);
  const [status, setStatus] = useState<Status | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<Preview | null>(null);

  const load = useCallback(async () => {
    try {
      const [settings, state] = await Promise.all([
        apiGet<Policy>(`${base}/settings`), apiGet<Status>(`${base}/status`),
      ]);
      setPolicy(settings);
      setStatus(state);
      setError("");
    } catch (err) { setError(err instanceof Error ? err.message : "Unable to load settings."); }
  }, []);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    let active = true;
    const timer = window.setInterval(() => {
      void apiGet<Status>(`${base}/status`).then((result) => {
        if (active) setStatus(result);
      }).catch(() => { if (active) setError("Unable to refresh storage status. Please reload."); });
    }, 30_000);
    return () => { active = false; window.clearInterval(timer); };
  }, []);

  function patch(value: Partial<Policy>) { setPolicy((current) => current ? { ...current, ...value } : current); }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!policy) return;
    setBusy(true);
    try {
      setPreview(await apiRequest<Preview>(`${base}/preview`, { method: "POST", body: JSON.stringify(policy) }));
    } catch (err) { toast.error({ message: err instanceof Error ? err.message : "Unable to load the preview." }); }
    finally { setBusy(false); }
  }

  async function save() {
    if (!policy) return;
    setBusy(true);
    try {
      const saved = await apiRequest<Policy>(`${base}/settings`, { method: "PUT", body: JSON.stringify(policy) });
      setPolicy(saved);
      setPreview(null);
      toast.success({ message: "Storage settings saved." });
      setStatus(await apiGet<Status>(`${base}/status`));
    } catch (err) { toast.error({ message: err instanceof Error ? err.message : "Unable to save settings." }); }
    finally { setBusy(false); }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-bold uppercase tracking-widest text-brand-600">Settings</p>
        <h1 className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">Data Storage</h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">Set how long data is kept and automatically remove the oldest eligible records.</p>
      </div>
      {error && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
        {error} <button type="button" className="ml-2 font-bold underline" onClick={() => void load()}>Reload</button>
      </div>}
      {!policy && !error && <p role="status" className="text-sm text-slate-500">Loading settings…</p>}
      {policy && <form onSubmit={(event) => void submit(event)} className={cardClass}>
        <fieldset disabled={busy || preview !== null} className="space-y-5 disabled:opacity-70">
          <label className="flex cursor-pointer items-center justify-between gap-4 border-b border-slate-100 pb-5 dark:border-slate-800">
            <span><span className="block font-semibold text-slate-900 dark:text-white">Automatic Cleanup (FIFO)</span>
              <span className="mt-1 block text-sm text-slate-500">The Worker runs cleanup at the selected interval.</span></span>
            <input aria-label="Automatic Cleanup" type="checkbox" className="size-5 accent-brand-500" checked={policy.enabled} onChange={(e) => patch({ enabled: e.target.checked })} />
          </label>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            <label className="text-sm font-semibold text-slate-700 dark:text-slate-200">Reading Retention (days)
              <input className={inputClass} type="number" min={1} max={3650} step={1} required value={policy.reading_retention_days} onChange={(e) => patch({ reading_retention_days: Number(e.target.value) })} />
              <span className="mt-2 block text-xs font-normal text-slate-500">Starts when a reading first enters the local database.</span>
            </label>
            <label className="text-sm font-semibold text-slate-700 dark:text-slate-200">Uploaded Log Retention (days)
              <input className={inputClass} type="number" min={1} max={3650} step={1} required value={policy.uploaded_log_retention_days} onChange={(e) => patch({ uploaded_log_retention_days: Number(e.target.value) })} />
              <span className="mt-2 block text-xs font-normal text-slate-500">Starts when a log is successfully uploaded.</span>
            </label>
            <label className="text-sm font-semibold text-slate-700 dark:text-slate-200">Cleanup Interval (minutes)
              <input className={inputClass} type="number" min={1} max={1440} step={1} required value={policy.cleanup_interval_minutes} onChange={(e) => patch({ cleanup_interval_minutes: Number(e.target.value) })} />
            </label>
          </div>
          <label className="flex items-center gap-3 text-sm font-semibold text-slate-700 dark:text-slate-200">
            <input className="size-4 accent-brand-500" type="checkbox" checked={policy.max_reading_records !== null} onChange={(e) => patch({ max_reading_records: e.target.checked ? 1_000_000 : null })} />
            Limit Reading Count
          </label>
          {policy.max_reading_records !== null && <label className="block max-w-md text-sm font-semibold text-slate-700 dark:text-slate-200">Maximum Reading Records
            <input className={inputClass} type="number" min={100} max={100_000_000} step={1} required value={policy.max_reading_records} onChange={(e) => patch({ max_reading_records: Number(e.target.value) })} />
            <span className="mt-2 block text-xs font-normal text-slate-500">When the limit is exceeded, the oldest uploaded readings may be removed before their retention period ends.</span>
          </label>}
          <p className="rounded-lg bg-slate-50 p-4 text-sm leading-6 text-slate-600 dark:bg-slate-950 dark:text-slate-300">
            Readings without confirmed delivery, queued messages, and readings linked to alarms remain protected. Sensor master data and configuration are retained.
          </p>
          <div className="flex justify-end"><button type="submit" className="rounded-lg bg-brand-500 px-5 py-3 text-sm font-semibold text-white hover:bg-brand-600">{busy ? "Processing…" : "Review & Save"}</button></div>
        </fieldset>
      </form>}
      {status && <section className={cardClass}>
        <h2 className="font-bold text-slate-900 dark:text-white">Storage Status</h2>
        <dl className="mt-4 grid gap-5 text-sm sm:grid-cols-2 lg:grid-cols-3">
          {[
            ["Stored Readings", count(status.total_readings)], ["Stored Logs", count(status.total_logs)],
            ["Pending Logs", count(status.pending_logs)], ["Oldest Reading", date(status.oldest_reading_at)],
            ["Last Successful Cleanup", date(status.last_cleanup_at)],
            ["Next Scheduled Check", status.cleanup_enabled ? (status.next_cleanup_at ? date(status.next_cleanup_at) : "Waiting for Worker") : "Disabled"],
          ].map(([label, value]) => <div key={label}><dt className="text-slate-500">{label}</dt><dd className="mt-1 font-semibold text-slate-900 dark:text-white">{value}</dd></div>)}
        </dl>
        <p className="mt-5 text-sm text-slate-500">Deleted in the last run: {count(status.deleted_readings)} readings and {count(status.deleted_logs)} logs.</p>
        {status.cleanup_enabled && status.cleanup_pending && <p className="mt-3 text-sm text-slate-500">Cleanup is continuing in batches.</p>}
        {status.capacity_exceeded && <p role="status" className="mt-3 text-sm text-amber-700 dark:text-amber-400">The reading count exceeds the limit. The Worker cleans up in batches; protected data is retained even when the limit is exceeded.</p>}
        {status.last_error && <p role="alert" className="mt-3 text-sm text-red-600">The last cleanup failed. Check the Worker status or contact your administrator.</p>}
      </section>}
      <ConfirmModal isOpen={preview !== null} isLoading={busy} title="Save Storage Rules?"
        message={preview ? `${policy?.enabled ? `With these rules, approximately ${count(preview.readings_to_delete)} readings and ${count(preview.logs_to_delete)} logs currently qualify for deletion in batches by the Worker. Deleted data cannot be recovered through the application.` : "Automatic cleanup will be disabled."} ${count(preview.protected_readings)} readings will remain protected. Counts may change as new data arrives.` : ""}
        confirmText="Save Rules" cancelText="Back" isDestructive={!!policy?.enabled && !!preview && preview.readings_to_delete + preview.logs_to_delete > 0}
        onClose={() => { if (!busy) setPreview(null); }} onConfirm={() => void save()} />
    </div>
  );
}
