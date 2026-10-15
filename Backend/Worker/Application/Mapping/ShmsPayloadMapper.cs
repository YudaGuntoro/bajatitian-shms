using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using Worker.Domain.Models;
using Worker.Domain.Payloads;
using Worker.Shared;

namespace Worker.Application.Mapping;

public static class ShmsPayloadMapper
{
    public static string ResolveDeviceCode(string topic, string payload)
    {
        // Raw logging must also work for malformed payloads.
        if (payload.TrimStart().StartsWith('{'))
        {
            try
            {
                var data = JObject.Parse(payload);
                var device = ReadString(data, "device_id", "deviceId", "device_code", "deviceCode", "device", "serial_number");
                if (!string.IsNullOrWhiteSpace(device)) return device;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Keep the original payload in the log even if it cannot be mapped.
            }
        }
        return DeviceFromTopic(topic, SensorFromTopic(topic));
    }

    public static IReadOnlyList<ShmsSensorReading> ToSensorReadings(string topic, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new FormatException("MQTT payload is empty.");
        }

        var trimmedPayload = payload.Trim();
        if (trimmedPayload.StartsWith('{'))
        {
            return [ToJsonSensorReading(topic, trimmedPayload)];
        }

        return ToRawCsvSensorReadings(topic, trimmedPayload);
    }

    public static ShmsSensorReading ToSensorReading(string topic, string payload)
    {
        var readings = ToSensorReadings(topic, payload);
        return readings.Count == 1
            ? readings[0]
            : throw new FormatException("MQTT payload contains multiple sensor readings.");
    }

    private static ShmsSensorReading ToJsonSensorReading(string topic, string payload)
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
        var topicDevice = DeviceCodeSegmentFromTopic(topic, sensorCode);
        if (!string.IsNullOrWhiteSpace(topicDevice))
        {
            return topicDevice;
        }

        var machineName = SignalHelper.TopicToMachineName(topic);
        return string.IsNullOrWhiteSpace(machineName)
            ? $"SHMS-{sensorCode}-01"
            : machineName;
    }

    private static string? DeviceCodeSegmentFromTopic(string topic, string sensorCode)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var normalizedSensor = sensorCode.ToLowerInvariant();
        var segments = topic
            .Trim()
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse();

        foreach (var segment in segments)
        {
            if (Regex.IsMatch(segment, $"^{Regex.Escape(normalizedSensor)}[-_]?\\d+$", RegexOptions.IgnoreCase))
            {
                return segment;
            }
        }

        return null;
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
        var suffix = string.IsNullOrWhiteSpace(axis) ? measurementName : $"{measurementName}-{axis}";
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

    private static IReadOnlyList<ShmsSensorReading> ToRawCsvSensorReadings(string topic, string payload)
    {
        var values = payload
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (values.Length == 2)
        {
            return ToVibratingWireReadings(topic, payload, values);
        }

        if (values.Length == 4)
        {
            return ToAccelerometerReadings(topic, payload, values);
        }

        if (values.Length == 7)
        {
            return ToTiltReadings(topic, payload, values);
        }

        if (values.Length == 6)
        {
            return ToAtrhReadings(topic, payload, values);
        }

        throw new FormatException($"Unsupported raw MQTT payload format. Expected 2 fields for VW, 4 fields for ACC, 6 fields for ATRH, or 7 fields for TILT, got {values.Length}.");
    }

    private static IReadOnlyList<ShmsSensorReading> ToVibratingWireReadings(
        string topic,
        string payload,
        IReadOnlyList<string> values)
    {
        var measuredAt = ParseDateTime(values[0]);
        var deviceCode = DeviceFromTopic(topic, "VW");

        return
        [
            CreateReading(deviceCode, "VW", "VW-FREQUENCY", "frequency", string.Empty, ParseDecimal(values[1]), "Hz", "frequency", measuredAt, payload)
        ];
    }

    private static IReadOnlyList<ShmsSensorReading> ToAccelerometerReadings(
        string topic,
        string payload,
        IReadOnlyList<string> values)
    {
        var measuredAt = ParseDateTime(values[0]);
        var deviceCode = DeviceFromTopic(topic, "ACC");

        return
        [
            CreateReading(deviceCode, "ACC", "ACC-ACCELERATION-X", "acceleration", "X", ParseDecimal(values[1]), "count", "acceleration", measuredAt, payload),
            CreateReading(deviceCode, "ACC", "ACC-ACCELERATION-Y", "acceleration", "Y", ParseDecimal(values[2]), "count", "acceleration", measuredAt, payload),
            CreateReading(deviceCode, "ACC", "ACC-ACCELERATION-Z", "acceleration", "Z", ParseDecimal(values[3]), "count", "acceleration", measuredAt, payload)
        ];
    }

    private static IReadOnlyList<ShmsSensorReading> ToTiltReadings(
        string topic,
        string payload,
        IReadOnlyList<string> values)
    {
        var measuredAt = ParseDateTime(values[0]);
        var deviceCode = DeviceFromTopic(topic, "TILT");

        return
        [
            CreateReading(deviceCode, "TILT", "TILT-TILT-X", "tilt", "X", ParseDecimal(values[1]), "deg", "angle", measuredAt, payload),
            CreateReading(deviceCode, "TILT", "TILT-TILT-Y", "tilt", "Y", ParseDecimal(values[2]), "deg", "angle", measuredAt, payload),
            CreateReading(deviceCode, "TILT", "TILT-TEMPERATURE-X", "temperature", "X", ParseDecimal(values[3]), "C", "temperature", measuredAt, payload),
            CreateReading(deviceCode, "TILT", "TILT-TEMPERATURE-Y", "temperature", "Y", ParseDecimal(values[4]), "C", "temperature", measuredAt, payload),
            CreateReading(deviceCode, "TILT", "TILT-RAW-X", "raw", "X", ParseDecimal(values[5]), "count", "raw", measuredAt, payload),
            CreateReading(deviceCode, "TILT", "TILT-RAW-Y", "raw", "Y", ParseDecimal(values[6]), "count", "raw", measuredAt, payload)
        ];
    }

    private static IReadOnlyList<ShmsSensorReading> ToAtrhReadings(
        string topic,
        string payload,
        IReadOnlyList<string> values)
    {
        var measuredAt = ParseDateTime(values[0]);
        var deviceCode = DeviceFromTopic(topic, "ATRH");
        var humidityRaw = ParseDecimal(values[2]);
        var humidity = humidityRaw > 100 ? humidityRaw / 10 : humidityRaw;

        return
        [
            CreateReading(deviceCode, "ATRH", "ATRH-TEMPERATURE", "temperature", string.Empty, ParseDecimal(values[1]), "C", "temperature", measuredAt, payload),
            CreateReading(deviceCode, "ATRH", "ATRH-HUMIDITY", "humidity", string.Empty, humidity, "%RH", "humidity", measuredAt, payload),
            CreateReading(deviceCode, "ATRH", "ATRH-RAW-HUMIDITY", "raw_humidity", string.Empty, humidityRaw, "count", "raw", measuredAt, payload),
            CreateReading(deviceCode, "ATRH", "ATRH-RAW-1", "raw", "1", ParseDecimal(values[3]), "count", "raw", measuredAt, payload),
            CreateReading(deviceCode, "ATRH", "ATRH-RAW-2", "raw", "2", ParseDecimal(values[4]), "count", "raw", measuredAt, payload),
            CreateReading(deviceCode, "ATRH", "ATRH-RAW-3", "raw", "3", ParseDecimal(values[5]), "count", "raw", measuredAt, payload)
        ];
    }

    private static ShmsSensorReading CreateReading(
        string deviceCode,
        string sensorCode,
        string channelCode,
        string measurementName,
        string axis,
        decimal value,
        string unitSymbol,
        string unitCategory,
        DateTime measuredAt,
        string rawPayload)
    {
        var reading = new ShmsSensorReading
        {
            DeviceCode = deviceCode,
            SensorCode = sensorCode,
            ChannelCode = channelCode,
            MeasurementName = measurementName,
            Axis = axis,
            UnitSymbol = unitSymbol,
            UnitCategory = unitCategory,
            NumericValue = value,
            MeasuredAt = measuredAt,
            QualityCode = "good",
            RawPayload = rawPayload
        };

        Validate(reading);
        return reading;
    }

    private static DateTime ParseDateTime(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid MQTT timestamp: {value}");

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid MQTT numeric value: {value}");

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
