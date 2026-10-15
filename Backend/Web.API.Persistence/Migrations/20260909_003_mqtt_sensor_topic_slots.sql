UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'TILT01'
SET legacy.code = 'TILT01', legacy.name = 'Tilt Sensor 01'
WHERE legacy.code = 'TILT' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'VW01'
SET legacy.code = 'VW01', legacy.name = 'Vibrating Wire Sensor 01'
WHERE legacy.code = 'VW' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ATRH01'
SET legacy.code = 'ATRH01', legacy.name = 'Air Temperature & RH Sensor 01'
WHERE legacy.code = 'ATRH' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ACC01'
SET legacy.code = 'ACC01', legacy.name = 'Accelerometer Sensor 01'
WHERE legacy.code = 'ACC' AND slot.id IS NULL;
