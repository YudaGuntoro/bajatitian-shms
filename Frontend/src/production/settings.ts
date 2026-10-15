import { apiGet, apiRequest } from "@/lib/api";

export type BackupSchedule = "daily" | "weekly" | "monthly";

export type TimezoneOption = "Asia/Jakarta" | "Asia/Bangkok" | "UTC";

export type SystemSettings = {
  accUnit: string;
  atrhUnit: string;
  autoCleanupEnabled: boolean;
  backupDbLocation: string;
  bufferWarningLimit: number;
  endpointDownWarningSeconds: number;
  logRetentionDays: number;
  mainApiEndpoint: string;
  mainApiToken: string;
  maxBufferRecords: number;
  maxRetry: number;
  retryDelaySeconds: number;
  schedule: BackupSchedule;
  sensorOfflineSeconds: number;
  tiltUnit: string;
  timezone: TimezoneOption;
  uploadBatchSize: number;
  uploadEnabled: boolean;
  uploadFailedWarningLimit: number;
  uploadIntervalSeconds: number;
  uploadTimeoutSeconds: number;
  vwUnit: string;
};

export const SYSTEM_SETTINGS_STORAGE_KEY = "shms-system-backup-settings";

export const defaultSystemSettings: SystemSettings = {
  accUnit: "g",
  atrhUnit: "C / %RH",
  autoCleanupEnabled: true,
  backupDbLocation: "",
  bufferWarningLimit: 1000,
  endpointDownWarningSeconds: 300,
  logRetentionDays: 30,
  mainApiEndpoint: "",
  mainApiToken: "",
  maxBufferRecords: 50000,
  maxRetry: 5,
  retryDelaySeconds: 30,
  schedule: "daily",
  sensorOfflineSeconds: 60,
  tiltUnit: "deg",
  timezone: "Asia/Jakarta",
  uploadBatchSize: 100,
  uploadEnabled: true,
  uploadFailedWarningLimit: 10,
  uploadIntervalSeconds: 10,
  uploadTimeoutSeconds: 15,
  vwUnit: "Hz",
};

type ApiSystemSettings = {
  backup_db_location: string;
  backup_schedule: BackupSchedule;
};

function fromApiSettings(settings: ApiSystemSettings, fallback: SystemSettings = defaultSystemSettings): SystemSettings {
  return {
    ...fallback,
    backupDbLocation: settings.backup_db_location ?? "",
    schedule: settings.backup_schedule ?? fallback.schedule,
  };
}

function toApiSettings(settings: SystemSettings) {
  return {
    backup_db_location: settings.backupDbLocation,
    backup_schedule: settings.schedule,
  };
}

export function readSystemSettings(): SystemSettings {
  if (typeof window === "undefined") {
    return defaultSystemSettings;
  }

  try {
    const stored = window.localStorage.getItem(SYSTEM_SETTINGS_STORAGE_KEY);
    const values = stored ? JSON.parse(stored) : {};
    // Only keep current settings when reading browser data saved by an older version.
    const current = Object.fromEntries(Object.entries(values).filter(([key]) =>
      Object.prototype.hasOwnProperty.call(defaultSystemSettings, key)));
    return { ...defaultSystemSettings, ...current };
  } catch {
    return defaultSystemSettings;
  }
}

export function saveSystemSettings(settings: SystemSettings) {
  window.localStorage.setItem(SYSTEM_SETTINGS_STORAGE_KEY, JSON.stringify(settings));
}

export async function fetchSystemSettings() {
  const localSettings = readSystemSettings();

  try {
    const settings = fromApiSettings(await apiGet<ApiSystemSettings>("/api/shms-system/settings"), localSettings);
    saveSystemSettings(settings);
    return settings;
  } catch {
    return localSettings;
  }
}

export async function updateSystemSettings(settings: SystemSettings) {
  const updated = fromApiSettings(await apiRequest<ApiSystemSettings>("/api/shms-system/settings", {
    body: JSON.stringify(toApiSettings(settings)),
    method: "PUT",
  }), settings);
  saveSystemSettings(updated);
  return updated;
}

export function displayNumber(value: number, fractionDigits = 2) {
  return Number(value).toFixed(fractionDigits);
}
