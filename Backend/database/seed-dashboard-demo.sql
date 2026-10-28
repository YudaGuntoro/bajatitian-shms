-- Run manually against the local development database only.
-- Adds one sample per channel for enabled MQTT sensor slots.
-- Existing configuration, devices, channels and readings are preserved.
-- Samples are marked as demo data and excluded from the upload queue.
START TRANSACTION;
SET @demo_time = CURRENT_TIMESTAMP(6);

CREATE TEMPORARY TABLE dashboard_demo_slots AS
SELECT topics.code, topics.name, topics.topic, types.id AS sensor_type_id,
       types.sensor_code, CAST(RIGHT(topics.code, 2) AS UNSIGNED) AS slot_number,
       CONCAT('SHMS-', types.sensor_code, '-', RIGHT(topics.code, 2)) AS device_code
FROM mqtt_sensor_topics topics
JOIN sensor_types types ON types.sensor_code = LEFT(topics.code, LENGTH(topics.code) - 2)
WHERE topics.enabled = 1 AND topics.code REGEXP '^(TILT|VW|ATRH|ACC)[0-9]{2}$';

INSERT INTO sensor_devices (site_id, sensor_type_id, device_code, device_name, status)
SELECT (SELECT id FROM shms_sites ORDER BY id LIMIT 1), slots.sensor_type_id,
       slots.device_code, CONCAT(slots.name, ' (Demo)'), 'active'
FROM dashboard_demo_slots slots
WHERE NOT EXISTS (SELECT 1 FROM sensor_devices existing WHERE existing.device_code = slots.device_code);

CREATE TEMPORARY TABLE dashboard_demo_channels (
    sensor_code VARCHAR(10), channel_code VARCHAR(80), channel_name VARCHAR(150),
    measurement_name VARCHAR(100), axis VARCHAR(10), unit_category VARCHAR(50), unit_symbol VARCHAR(20)
);
INSERT INTO dashboard_demo_channels VALUES
('TILT', 'TILT-X', 'Tilt X Axis', 'tilt', 'X', 'angle', 'deg'),
('TILT', 'TILT-Y', 'Tilt Y Axis', 'tilt', 'Y', 'angle', 'deg'),
('VW', 'VW-FREQ', 'Vibrating Wire Frequency', 'frequency', NULL, 'frequency', 'Hz'),
('ATRH', 'AIR-TEMP', 'Air Temperature', 'temperature', NULL, 'temperature', 'C'),
('ATRH', 'AIR-RH', 'Relative Humidity', 'humidity', NULL, 'humidity', '%RH'),
('ACC', 'ACC-X', 'Acceleration X Axis', 'acceleration', 'X', 'acceleration', 'g'),
('ACC', 'ACC-Y', 'Acceleration Y Axis', 'acceleration', 'Y', 'acceleration', 'g'),
('ACC', 'ACC-Z', 'Acceleration Z Axis', 'acceleration', 'Z', 'acceleration', 'g');

INSERT INTO sensor_channels
    (sensor_device_id, unit_id, channel_code, channel_name, measurement_name, axis, sampling_interval_seconds, is_active)
SELECT devices.id, units.id, template.channel_code, template.channel_name,
       template.measurement_name, template.axis, 60, 1
FROM dashboard_demo_slots slots
JOIN sensor_devices devices ON devices.device_code = slots.device_code
JOIN dashboard_demo_channels template ON template.sensor_code = slots.sensor_code
JOIN measurement_units units ON units.unit_category = template.unit_category AND units.unit_symbol = template.unit_symbol
WHERE NOT EXISTS (
    SELECT 1 FROM sensor_channels existing
    WHERE existing.sensor_device_id = devices.id AND existing.channel_code = template.channel_code
);

INSERT INTO sensor_readings
    (sensor_channel_id, measured_at, numeric_value, quality_code, raw_payload, ingested_at, uploaded_at)
SELECT channels.id, @demo_time,
       CASE
           WHEN template.measurement_name = 'tilt' THEN ROUND(slots.slot_number * IF(template.axis = 'X', 0.023, -0.017), 4)
           WHEN template.measurement_name = 'frequency' THEN 1250 + slots.slot_number * 12.75
           WHEN template.measurement_name = 'temperature' THEN 28 + slots.slot_number * 0.35
           WHEN template.measurement_name = 'humidity' THEN 62 + slots.slot_number * 1.2
           WHEN template.axis = 'Z' THEN 1 + slots.slot_number * 0.001
           WHEN template.axis = 'X' THEN slots.slot_number * 0.002
           ELSE slots.slot_number * -0.0015
       END,
       'good', JSON_OBJECT('source', 'dashboard-demo', 'demo', TRUE, 'sensor', slots.code, 'topic', slots.topic),
       @demo_time, @demo_time
FROM dashboard_demo_slots slots
JOIN sensor_devices devices ON devices.device_code = slots.device_code
JOIN dashboard_demo_channels template ON template.sensor_code = slots.sensor_code
JOIN sensor_channels channels ON channels.sensor_device_id = devices.id AND channels.channel_code = template.channel_code;

COMMIT;
DROP TEMPORARY TABLE dashboard_demo_channels;
DROP TEMPORARY TABLE dashboard_demo_slots;

SELECT COUNT(*) AS demo_readings, COUNT(DISTINCT channels.sensor_device_id) AS demo_sensors
FROM sensor_readings readings
JOIN sensor_channels channels ON channels.id = readings.sensor_channel_id
WHERE readings.measured_at = @demo_time AND JSON_UNQUOTE(JSON_EXTRACT(readings.raw_payload, '$.source')) = 'dashboard-demo';
