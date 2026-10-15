using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Worker.Configuration;

namespace Worker.Infrastructure.Mqtt;

public sealed class MqttTopicConfigService
{
	private readonly ILogger<MqttTopicConfigService> _logger;

	public MqttTopicConfigService(ILogger<MqttTopicConfigService> logger)
	{
		_logger = logger;
	}

	public async Task<IReadOnlyList<MqttSubscriptionTopic>> GetActiveTopicsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			IReadOnlyList<MqttSubscriptionTopic> result;
			await using (MySqlConnection connection = new MySqlConnection(DatabaseConfig.MysqlConnString))
			{
				await connection.OpenAsync(cancellationToken);
				await EnsureMqttSensorTopicsTableAsync(connection, cancellationToken);
				IReadOnlyList<MqttSubscriptionTopic> readOnlyList2;
				await using (MySqlCommand command = connection.CreateCommand())
				{
					command.CommandText = "\nSELECT topic, qos\nFROM mqtt_sensor_topics\nWHERE enabled = 1\n  AND topic IS NOT NULL\n  AND TRIM(topic) <> ''\nORDER BY\n    CASE\n        WHEN code LIKE 'TILT%' THEN 1\n        WHEN code LIKE 'VW%' THEN 2\n        WHEN code LIKE 'ATRH%' THEN 3\n        WHEN code LIKE 'ACC%' THEN 4\n        ELSE 5\n    END,\n    code;";
					List<MqttSubscriptionTopic> topics = new List<MqttSubscriptionTopic>();
					IReadOnlyList<MqttSubscriptionTopic> readOnlyList;
					await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						while (await reader.ReadAsync(cancellationToken))
						{
							topics.Add(new MqttSubscriptionTopic(reader.GetString(0).Trim(), Math.Clamp(reader.GetInt32(1), 0, 2)));
						}
						readOnlyList = (from x in topics.GroupBy<MqttSubscriptionTopic, string>((MqttSubscriptionTopic x) => x.Topic, StringComparer.OrdinalIgnoreCase)
							select x.First()).ToList();
					}
					readOnlyList2 = readOnlyList;
				}
				result = readOnlyList2;
			}
			return result;
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogWarning(ex, "[MQTT] Failed to load topic configuration from database. Falling back to Settings.ini.");
			return Array.Empty<MqttSubscriptionTopic>();
		}
	}

	public async Task<MqttBrokerOptions?> GetActiveBrokerConfigAsync(string? fallbackPassword = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			await using MySqlConnection connection = new MySqlConnection(DatabaseConfig.MysqlConnString);
			await connection.OpenAsync(cancellationToken);
			await EnsureMqttBrokerConfigsTableAsync(connection, cancellationToken);

			await using MySqlCommand command = connection.CreateCommand();
			command.CommandText = "\nSELECT host, port, client_id, username, use_tls\nFROM mqtt_broker_configs\nWHERE is_active = 1\nORDER BY id\nLIMIT 1;";

			await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
			{
				return null;
			}

			var host = reader.GetString(0).Trim();
			var port = Math.Clamp(reader.GetInt32(1), 1, 65535);
			var clientId = reader.GetString(2).Trim();
			var username = reader.IsDBNull(3) ? null : reader.GetString(3).Trim();
			var useTls = reader.GetBoolean(4);

			if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(clientId))
			{
				return null;
			}

			return new MqttBrokerOptions(host, port, clientId, username, fallbackPassword, useTls);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogWarning(ex, "[MQTT] Failed to load broker configuration from database. Falling back to Settings.ini.");
			return null;
		}
	}

	private static async Task EnsureMqttBrokerConfigsTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
	{
		await using MySqlCommand command = connection.CreateCommand();
		command.CommandText = "\nCREATE TABLE IF NOT EXISTS mqtt_broker_configs (\n    id INT AUTO_INCREMENT PRIMARY KEY,\n    config_name VARCHAR(100) NOT NULL,\n    host VARCHAR(255) NOT NULL,\n    port INT NOT NULL DEFAULT 1883,\n    client_id VARCHAR(100) NOT NULL,\n    username VARCHAR(100) NULL,\n    password_secret_name VARCHAR(150) NULL,\n    use_tls TINYINT(1) NOT NULL DEFAULT 0,\n    is_active TINYINT(1) NOT NULL DEFAULT 1,\n    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,\n    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n    UNIQUE KEY uq_mqtt_broker_configs_config_name (config_name),\n    INDEX ix_mqtt_broker_configs_is_active (is_active)\n);\n\nINSERT INTO mqtt_broker_configs\n    (config_name, host, port, client_id, use_tls, is_active)\nVALUES\n    ('default', 'localhost', 1883, 'SHMSClient', 0, 1)\nON DUPLICATE KEY UPDATE\n    host = COALESCE(NULLIF(host, ''), VALUES(host)),\n    port = IF(port BETWEEN 1 AND 65535, port, VALUES(port)),\n    client_id = COALESCE(NULLIF(client_id, ''), VALUES(client_id)),\n    updated_at = CURRENT_TIMESTAMP;";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task EnsureMqttSensorTopicsTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
	{
		await using MySqlCommand command = connection.CreateCommand();
		command.CommandText = "\nCREATE TABLE IF NOT EXISTS mqtt_sensor_topics (\n    id INT AUTO_INCREMENT PRIMARY KEY,\n    code VARCHAR(20) NOT NULL,\n    name VARCHAR(100) NOT NULL,\n    topic VARCHAR(255) NOT NULL,\n    qos INT NOT NULL DEFAULT 1,\n    enabled TINYINT(1) NOT NULL DEFAULT 1,\n    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,\n    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n    UNIQUE KEY uq_mqtt_sensor_topics_code (code),\n    INDEX ix_mqtt_sensor_topics_enabled (enabled)\n);\n\nUPDATE mqtt_sensor_topics legacy\nLEFT JOIN mqtt_sensor_topics slot ON slot.code = 'TILT01'\nSET legacy.code = 'TILT01', legacy.name = 'Tilt Sensor 01'\nWHERE legacy.code = 'TILT' AND slot.id IS NULL;\n\nUPDATE mqtt_sensor_topics legacy\nLEFT JOIN mqtt_sensor_topics slot ON slot.code = 'VW01'\nSET legacy.code = 'VW01', legacy.name = 'Vibrating Wire Sensor 01'\nWHERE legacy.code = 'VW' AND slot.id IS NULL;\n\nUPDATE mqtt_sensor_topics legacy\nLEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ATRH01'\nSET legacy.code = 'ATRH01', legacy.name = 'Air Temperature & RH Sensor 01'\nWHERE legacy.code = 'ATRH' AND slot.id IS NULL;\n\nUPDATE mqtt_sensor_topics legacy\nLEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ACC01'\nSET legacy.code = 'ACC01', legacy.name = 'Accelerometer Sensor 01'\nWHERE legacy.code = 'ACC' AND slot.id IS NULL;";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
