"use client";

import React, { FormEvent, useState } from "react";
import PageBreadcrumb from "@/components/common/PageBreadCrumb";
import LocationMap from "@/components/location/LocationMap";
import { useToast } from "@/context/ToastContext";
import {
  parseMapCoordinates,
  readBridgeLocationSettings,
  saveBridgeLocationSettings,
  type BridgeLocationSettings,
} from "./locationSettings";

const inputClass = "mt-2 h-11 w-full rounded-lg border border-slate-300 bg-white px-4 text-sm font-bold text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-3 focus:ring-brand-500/20 dark:border-slate-700 dark:bg-slate-950 dark:text-white dark:placeholder:text-slate-500";
const labelClass = "text-xs font-bold uppercase text-slate-600 dark:text-slate-300";

async function resolveMapLocation(query: string) {
  const parsed = parseMapCoordinates(query);
  if (parsed) {
    return parsed;
  }

  const response = await fetch(
    `https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${encodeURIComponent(query)}`
  );

  if (!response.ok) {
    throw new Error("Failed to search map location.");
  }

  const results = (await response.json()) as Array<{ lat: string; lon: string }>;
  const first = results[0];
  if (!first) {
    throw new Error("Map location not found.");
  }

  return {
    latitude: Number(first.lat),
    longitude: Number(first.lon),
  };
}

export default function LocationPage() {
  const toast = useToast();
  const [settings, setSettings] = useState<BridgeLocationSettings>(() => readBridgeLocationSettings());
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!settings.area.trim()) {
      toast.error({ message: "Area wajib diisi." });
      return;
    }

    if (!settings.bridgeName.trim()) {
      toast.error({ message: "Bridge Name wajib diisi." });
      return;
    }

    if (!settings.mapQuery.trim()) {
      toast.error({ message: "Map location wajib diisi." });
      return;
    }

    setSaving(true);
    try {
      const location = await resolveMapLocation(settings.mapQuery);
      const updated = {
        ...settings,
        area: settings.area.trim(),
        bridgeName: settings.bridgeName.trim(),
        latitude: location.latitude,
        longitude: location.longitude,
        mapQuery: settings.mapQuery.trim(),
      };
      setSettings(updated);
      saveBridgeLocationSettings(updated);
      toast.success({ message: "Bridge location saved." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to save bridge location." });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageBreadcrumb pageTitle="Location" />

      <form
        className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-slate-900 sm:p-6"
        onSubmit={(event) => void submit(event)}
      >
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.4fr)_auto] lg:items-end">
          <label className={labelClass}>
            Area
            <input
              className={inputClass}
              onChange={(event) => setSettings((current) => ({ ...current, area: event.target.value }))}
              placeholder="Kalimantan Tengah"
              value={settings.area}
            />
          </label>

          <label className={labelClass}>
            Bridge Name
            <input
              className={inputClass}
              onChange={(event) => setSettings((current) => ({ ...current, bridgeName: event.target.value }))}
              placeholder="Bridge KM 12"
              value={settings.bridgeName}
            />
          </label>

          <label className={labelClass}>
            Map Location
            <input
              className={inputClass}
              onChange={(event) => setSettings((current) => ({ ...current, mapQuery: event.target.value }))}
              placeholder="-0.789275, 113.921327 atau paste Google Maps URL"
              value={settings.mapQuery}
            />
          </label>

          <button
            className="h-11 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white transition hover:bg-brand-600 disabled:opacity-60"
            disabled={saving}
            type="submit"
          >
            {saving ? "Saving" : "Save"}
          </button>
        </div>
      </form>

      <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-slate-900 sm:p-6">
        <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Bridge Location Map</h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              {settings.area || "-"} / {settings.bridgeName || "-"}
            </p>
          </div>
          <div className="rounded-md bg-slate-100 px-3 py-2 text-xs font-black text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            {settings.latitude.toFixed(6)}, {settings.longitude.toFixed(6)}
          </div>
        </div>
        
        <LocationMap
          bridgeName={settings.bridgeName || "Bridge Location"}
          center={[settings.latitude, settings.longitude]}
          zoom={settings.zoom}
        />
      </div>
    </div>
  );
}
