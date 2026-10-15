using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SHMS.Storage;

public sealed class RetentionSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    [Range(1, 3650), JsonPropertyName("reading_retention_days")]
    public int ReadingRetentionDays { get; set; } = 30;
    [Range(1, 3650), JsonPropertyName("uploaded_log_retention_days")]
    public int UploadedLogRetentionDays { get; set; } = 7;
    [Range(1, 1440), JsonPropertyName("cleanup_interval_minutes")]
    public int CleanupIntervalMinutes { get; set; } = 60;
    [Range(100, 100_000_000), JsonPropertyName("max_reading_records")]
    public long? MaxReadingRecords { get; set; }

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);
}

public sealed class RetentionPreview
{
    [JsonPropertyName("readings_to_delete")]
    public long ReadingsToDelete { get; set; }
    [JsonPropertyName("logs_to_delete")]
    public long LogsToDelete { get; set; }
    [JsonPropertyName("protected_readings")]
    public long ProtectedReadings { get; set; }
}

public sealed class RetentionStatus
{
    [JsonPropertyName("cleanup_enabled")]
    public bool CleanupEnabled { get; set; }
    [JsonPropertyName("cleanup_pending")]
    public bool CleanupPending { get; set; }
    [JsonPropertyName("total_readings")]
    public long TotalReadings { get; set; }
    [JsonPropertyName("total_logs")]
    public long TotalLogs { get; set; }
    [JsonPropertyName("pending_logs")]
    public long PendingLogs { get; set; }
    [JsonPropertyName("oldest_reading_at")]
    public DateTime? OldestReadingAt { get; set; }
    [JsonPropertyName("last_cleanup_at")]
    public DateTime? LastCleanupAt { get; set; }
    [JsonPropertyName("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }
    [JsonPropertyName("next_cleanup_at")]
    public DateTime? NextCleanupAt { get; set; }
    [JsonPropertyName("deleted_readings")]
    public long DeletedReadings { get; set; }
    [JsonPropertyName("deleted_logs")]
    public long DeletedLogs { get; set; }
    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }
    [JsonPropertyName("capacity_exceeded")]
    public bool CapacityExceeded { get; set; }
}
