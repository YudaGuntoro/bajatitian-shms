using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Web.API.Domain.Production;

public class LogBuffer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("sensor_device_id")]
    public long? SensorDeviceId { get; set; }

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(255)]
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [Required]
    [Column(TypeName = "longtext")]
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("time_stamp")]
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; }

    [Column(TypeName = "text")]
    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("uploaded_at")]
    public DateTime? UploadedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
