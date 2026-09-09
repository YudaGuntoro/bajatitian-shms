using Newtonsoft.Json.Linq;
using Worker.Domain.Models;
using Worker.Domain.Payloads;
using Worker.Shared;

namespace Worker.Application.Mapping;

public static class ShmsPayloadMapper
{
    public static ShmsSensorReading ToSensorReading(string topic, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new FormatException("MQTT payload is empty.");
        }

        var shmsPayload = new ShmsPayload
        {
            Topic = topic,
            Data = ParsePayload(payload)
        };

        var sensorCode = FirstText(
            ReadString(shmsPayload.Data, "sensor_code", "sensorCode", "sensor", "type"),
            SensorFromTopic(topic));
        var deviceCode = FirstText(
            ReadString(shmsPayload.Data, "device_id", "deviceId", "device_code", "deviceCode", "device", "serial_number"),
            DeviceFromTopic(topic, sensorCode));
        var axis = ReadString(shmsPayload.Data, "axis", "Axis");
        var measurementName = FirstText(
            ReadString(shmsPayload.Data, "measurement_name", "measurementName", "measurement", "metric"),
            MeasurementFromSensor(sensorCode));
        var channelCode = FirstText(
            ReadString(shmsPayload.Data, "channel_code", "channelCode", "channel", "channel_no"),
            BuildChannelCode(sensorCode, measurementName, axis));
        var value = ReadDecimal(shmsPayload.Data, "value", "numeric_value", "numericValue", measurementName, channelCode);
        var unitSymbol = FirstText(ReadString(shmsPayload.Data, "unit", "unit_symbol", "unitSymbol"), DefaultUnitSymbol(sensorCode, measurementName));
        var unitCategory = FirstText(ReadString(shmsPayload.Data, "unit_category", "unitCategory"), DefaultUnitCategory(sensorCode, measurementName));

        if (!value.HasValue)
        {
            throw new FormatException("MQTT payload missing numeric value.");
        }

        var reading = new ShmsSensorReading
        {
            DeviceCode = deviceCode,
            SensorCode = sensorCode,
            ChannelCode = channelCode,
            MeasurementName = measurementName,
            Axis = string.IsNullOrWhiteSpace(axis) ? null : axis.Trim().ToUpperInvariant(),
            UnitSymbol = unitSymbol,
            UnitCategory = unitCategory,
            NumericValue = value.Value,
            MeasuredAt = ReadDateTime(shmsPayload.Data, "measured_at", "measuredAt", "timestamp", "time_stamp", "ts") ?? DateTime.Now,
            QualityCode = NormalizeQuality(ReadString(shmsPayload.Data, "quality_code", "qualityCode", "quality")),
            RawPayload = payload
        };

        Validate(reading);
        return reading;
    }

    private static JObject ParsePayload(string payload)
    {
        var token = JToken.Parse(payload);
        if (token is JObject obj)
        {
            return obj;
        }

        throw new FormatException("MQTT payload must be a JSON object.");
    }

    private static string? ReadString(JObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            var token = source.SelectToken(key, false) ?? source.GetValue(key, StringComparison.OrdinalIgnoreCase);
            var text = token?.Type == JTokenType.Null ? null : token?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(JObject source, params string[] keys)
    {
        foreach (var key in keys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            var token = source.SelectToken(key, false) ?? source.GetValue(key, StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (decimal.TryParse(token.ToString(), out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static DateTime? ReadDateTime(JObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            var token = source.SelectToken(key, false) ?? source.GetValue(key, StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (DateTime.TryParse(token.ToString(), out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string SensorFromTopic(string topic)
    {
        var text = topic.ToUpperInvariant();
        foreach (var code in new[] { "TILT", "VW", "ATRH", "ACC" })
        {
            if (text.Contains(code, StringComparison.Ordinal))
            {
                return code;
            }
        }

        return "GEN";
    }

    private static string DeviceFromTopic(string topic, string sensorCode)
    {
        var machineName = SignalHelper.TopicToMachineName(topic);
        return string.IsNullOrWhiteSpace(machineName)
            ? $"SHMS-{sensorCode}-01"
            : machineName;
    }

    private static string MeasurementFromSensor(string sensorCode) =>
        sensorCode.ToUpperInvariant() switch
        {
            "TILT" => "tilt",
            "VW" => "frequency",
            "ATRH" => "temperature",
            "ACC" => "acceleration",
            _ => "measurement"
        };

    private static string BuildChannelCode(string sensorCode, string measurementName, string? axis)
    {
        var suffix = string.IsNullOrWhiteSpace(axis) ? measurementName : axis;
        return $"{sensorCode}-{suffix}".ToUpperInvariant();
    }

    private static string DefaultUnitSymbol(string sensorCode, string measurementName) =>
        (sensorCode.ToUpperInvariant(), measurementName.ToLowerInvariant()) switch
        {
            ("TILT", _) => "deg",
            ("VW", _) => "Hz",
            ("ATRH", "humidity") => "%RH",
            ("ATRH", _) => "C",
            ("ACC", _) => "g",
            _ => "unit"
        };

    private static string DefaultUnitCategory(string sensorCode, string measurementName) =>
        (sensorCode.ToUpperInvariant(), measurementName.ToLowerInvariant()) switch
        {
            ("TILT", _) => "angle",
            ("VW", _) => "frequency",
            ("ATRH", "humidity") => "humidity",
            ("ATRH", _) => "temperature",
            ("ACC", _) => "acceleration",
            _ => "generic"
        };

    private static string NormalizeQuality(string? quality)
    {
        var normalized = string.IsNullOrWhiteSpace(quality) ? "good" : quality.Trim().ToLowerInvariant();
        return normalized is "good" or "suspect" or "bad" or "missing" ? normalized : "good";
    }

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void Validate(ShmsSensorReading reading)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(reading.SensorCode)) missing.Add("sensor_code");
        if (string.IsNullOrWhiteSpace(reading.DeviceCode)) missing.Add("device_id/device_code");
        if (string.IsNullOrWhiteSpace(reading.ChannelCode)) missing.Add("channel_code");
        if (string.IsNullOrWhiteSpace(reading.MeasurementName)) missing.Add("measurement_name");

        if (missing.Count > 0)
        {
            throw new FormatException($"MQTT payload missing/invalid fields: {string.Join(", ", missing)}.");
        }
    }
}
