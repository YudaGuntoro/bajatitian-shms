-- SHMS dummy data for one week around 2026-09-10.
-- Range: 2026-09-07 through 2026-09-13.

USE `bajatitian_shms`;

INSERT INTO sensor_readings
    (sensor_channel_id, measured_at, numeric_value, quality_code, raw_payload)
SELECT
    channels.id,
    TIMESTAMP(days.measured_date, slots.measured_time) AS measured_at,
    CASE channels.id
        WHEN 1 THEN ROUND(0.12 + (days.day_index * 0.015) + (slots.slot_index * 0.008), 8)
        WHEN 2 THEN ROUND(1280.00 + (days.day_index * 4.50) + (slots.slot_index * 1.75), 8)
        WHEN 3 THEN ROUND(29.80 + (days.day_index * 0.22) + (slots.slot_index * 0.35), 8)
        WHEN 4 THEN ROUND(66.00 + (days.day_index * 0.60) - (slots.slot_index * 0.40), 8)
        WHEN 5 THEN ROUND(0.98 + (days.day_index * 0.006) + (slots.slot_index * 0.012), 8)
        ELSE 0
    END AS numeric_value,
    CASE
        WHEN days.measured_date = '2026-09-10' AND slots.slot_index = 3 AND channels.id IN (1, 5) THEN 'suspect'
        WHEN days.measured_date = '2026-09-12' AND slots.slot_index = 4 AND channels.id = 4 THEN 'suspect'
        ELSE 'good'
    END AS quality_code,
    JSON_OBJECT(
        'source', 'dummy-week',
        'device_id', devices.device_code,
        'channel_code', channels.channel_code,
        'measurement', channels.measurement_name,
        'axis', channels.axis,
        'value', CASE channels.id
            WHEN 1 THEN ROUND(0.12 + (days.day_index * 0.015) + (slots.slot_index * 0.008), 8)
            WHEN 2 THEN ROUND(1280.00 + (days.day_index * 4.50) + (slots.slot_index * 1.75), 8)
            WHEN 3 THEN ROUND(29.80 + (days.day_index * 0.22) + (slots.slot_index * 0.35), 8)
            WHEN 4 THEN ROUND(66.00 + (days.day_index * 0.60) - (slots.slot_index * 0.40), 8)
            WHEN 5 THEN ROUND(0.98 + (days.day_index * 0.006) + (slots.slot_index * 0.012), 8)
            ELSE 0
        END,
        'measured_at', DATE_FORMAT(TIMESTAMP(days.measured_date, slots.measured_time), '%Y-%m-%d %H:%i:%s')
    ) AS raw_payload
FROM (
    SELECT 1 AS day_index, DATE('2026-09-07') AS measured_date
    UNION ALL SELECT 2, DATE('2026-09-08')
    UNION ALL SELECT 3, DATE('2026-09-09')
    UNION ALL SELECT 4, DATE('2026-09-10')
    UNION ALL SELECT 5, DATE('2026-09-11')
    UNION ALL SELECT 6, DATE('2026-09-12')
    UNION ALL SELECT 7, DATE('2026-09-13')
) days
CROSS JOIN (
    SELECT 1 AS slot_index, TIME('00:00:00') AS measured_time
    UNION ALL SELECT 2, TIME('06:00:00')
    UNION ALL SELECT 3, TIME('12:00:00')
    UNION ALL SELECT 4, TIME('18:00:00')
) slots
JOIN sensor_channels channels ON channels.id IN (1, 2, 3, 4, 5)
JOIN sensor_devices devices ON devices.id = channels.sensor_device_id
ON DUPLICATE KEY UPDATE
    numeric_value = VALUES(numeric_value),
    quality_code = VALUES(quality_code),
    raw_payload = VALUES(raw_payload);

INSERT INTO log_buffer
    (sensor_device_id, device_id, topic, payload, time_stamp, status, retry_count, last_error, uploaded_at, created_at)
SELECT
    devices.id AS sensor_device_id,
    devices.device_code AS device_id,
    CASE types.sensor_code
        WHEN 'TILT' THEN 'shms/tilt'
        WHEN 'VW' THEN 'shms/vw'
        WHEN 'ATRH' THEN 'shms/atrh'
        WHEN 'ACC' THEN 'shms/acc'
        ELSE 'shms/unknown'
    END AS topic,
    JSON_OBJECT(
        'source', 'dummy-week',
        'device_id', devices.device_code,
        'sensor', types.sensor_code,
        'measured_at', DATE_FORMAT(TIMESTAMP(days.measured_date, slots.measured_time), '%Y-%m-%d %H:%i:%s'),
        'status', CASE
            WHEN days.measured_date = '2026-09-10' AND slots.slot_index = 3 THEN 'failed'
            WHEN days.measured_date = '2026-09-12' AND slots.slot_index = 4 THEN 'pending'
            ELSE 'uploaded'
        END
    ) AS payload,
    TIMESTAMP(days.measured_date, slots.measured_time) AS time_stamp,
    CASE
        WHEN days.measured_date = '2026-09-10' AND slots.slot_index = 3 THEN 'failed'
        WHEN days.measured_date = '2026-09-12' AND slots.slot_index = 4 THEN 'pending'
        ELSE 'uploaded'
    END AS status,
    CASE
        WHEN days.measured_date = '2026-09-10' AND slots.slot_index = 3 THEN 3
        WHEN days.measured_date = '2026-09-12' AND slots.slot_index = 4 THEN 1
        ELSE 0
    END AS retry_count,
    CASE
        WHEN days.measured_date = '2026-09-10' AND slots.slot_index = 3 THEN 'Dummy outage to Witon Server'
        ELSE NULL
    END AS last_error,
    CASE
        WHEN days.measured_date IN ('2026-09-10', '2026-09-12') AND slots.slot_index IN (3, 4) THEN NULL
        ELSE TIMESTAMPADD(SECOND, 5, TIMESTAMP(days.measured_date, slots.measured_time))
    END AS uploaded_at,
    TIMESTAMP(days.measured_date, slots.measured_time) AS created_at
FROM (
    SELECT 1 AS day_index, DATE('2026-09-07') AS measured_date
    UNION ALL SELECT 2, DATE('2026-09-08')
    UNION ALL SELECT 3, DATE('2026-09-09')
    UNION ALL SELECT 4, DATE('2026-09-10')
    UNION ALL SELECT 5, DATE('2026-09-11')
    UNION ALL SELECT 6, DATE('2026-09-12')
    UNION ALL SELECT 7, DATE('2026-09-13')
) days
CROSS JOIN (
    SELECT 1 AS slot_index, TIME('00:00:00') AS measured_time
    UNION ALL SELECT 2, TIME('06:00:00')
    UNION ALL SELECT 3, TIME('12:00:00')
    UNION ALL SELECT 4, TIME('18:00:00')
) slots
JOIN sensor_devices devices ON devices.id IN (1, 2, 3, 4)
JOIN sensor_types types ON types.id = devices.sensor_type_id
WHERE NOT EXISTS (
    SELECT 1
    FROM log_buffer existing
    WHERE existing.device_id = devices.device_code
      AND existing.time_stamp = TIMESTAMP(days.measured_date, slots.measured_time)
      AND existing.topic = CASE types.sensor_code
          WHEN 'TILT' THEN 'shms/tilt'
          WHEN 'VW' THEN 'shms/vw'
          WHEN 'ATRH' THEN 'shms/atrh'
          WHEN 'ACC' THEN 'shms/acc'
          ELSE 'shms/unknown'
      END
);

INSERT INTO server_sync_status
    (id, server_name, endpoint_url, is_online, outage_started_at, last_success_at, last_failure_at, last_error, redis_buffer_count, db_spillover_count)
VALUES
    (1, 'Witon Server', NULL, 0, '2026-09-10 12:00:00.000000', '2026-09-10 11:59:30.000000', '2026-09-10 12:12:00.000000', 'Dummy outage to Witon Server', 8, 4)
ON DUPLICATE KEY UPDATE
    server_name = VALUES(server_name),
    endpoint_url = VALUES(endpoint_url),
    is_online = VALUES(is_online),
    outage_started_at = VALUES(outage_started_at),
    last_success_at = VALUES(last_success_at),
    last_failure_at = VALUES(last_failure_at),
    last_error = VALUES(last_error),
    redis_buffer_count = VALUES(redis_buffer_count),
    db_spillover_count = VALUES(db_spillover_count);
