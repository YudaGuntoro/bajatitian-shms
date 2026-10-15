-- MySQL 8.0+. Select the target database before running this migration.
-- Prerequisite: existing SHMS tables from bajatitian-shms_schema_only.sql.
-- Idempotent: detect equivalent indexes/foreign keys by columns, not only names.
-- No rows are deleted or rewritten. Duplicate keys/orphaned references make ALTER fail;
-- correct the offending source data and rerun. Do not disable FOREIGN_KEY_CHECKS.
-- DDL commits implicitly; take a backup and run during a maintenance window for large tables.

-- measurement_units: unit_category,unit_symbol (unique identity / retry deduplication)
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'measurement_units'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'unit_category,unit_symbol'
           AND SUM(SUB_PART IS NOT NULL) = 0
           AND MIN(NON_UNIQUE) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE measurement_units ADD UNIQUE INDEX uq_measurement_units_category_symbol (unit_category, unit_symbol)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- sensor_types: sensor_code (unique identity / retry deduplication)
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_types'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'sensor_code'
           AND SUM(SUB_PART IS NOT NULL) = 0
           AND MIN(NON_UNIQUE) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_types ADD UNIQUE INDEX uq_sensor_types_sensor_code (sensor_code)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- sensor_devices: device_code (unique identity / retry deduplication)
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_devices'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'device_code'
           AND SUM(SUB_PART IS NOT NULL) = 0
           AND MIN(NON_UNIQUE) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_devices ADD UNIQUE INDEX uq_sensor_devices_device_code (device_code)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- sensor_channels: sensor_device_id,channel_code (unique identity / retry deduplication)
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_channels'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'sensor_device_id,channel_code'
           AND SUM(SUB_PART IS NOT NULL) = 0
           AND MIN(NON_UNIQUE) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_channels ADD UNIQUE INDEX uq_sensor_channels_device_channel_code (sensor_device_id, channel_code)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- sensor_readings: sensor_channel_id,measured_at (unique identity / retry deduplication)
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_readings'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'sensor_channel_id,measured_at'
           AND SUM(SUB_PART IS NOT NULL) = 0
           AND MIN(NON_UNIQUE) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_readings ADD UNIQUE INDEX uq_sensor_readings_channel_measured_at (sensor_channel_id, measured_at)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- sensor_readings: measured_at
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_readings'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'measured_at'
           AND SUM(SUB_PART IS NOT NULL) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_readings ADD INDEX ix_sensor_readings_measured_at (measured_at)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- log_buffer: time_stamp
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'log_buffer'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'time_stamp'
           AND SUM(SUB_PART IS NOT NULL) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE log_buffer ADD INDEX ix_log_buffer_time_stamp (time_stamp)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- log_buffer: status,time_stamp
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'log_buffer'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'status,time_stamp'
           AND SUM(SUB_PART IS NOT NULL) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE log_buffer ADD INDEX ix_log_buffer_status_time_stamp (status, time_stamp)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- log_buffer: device_id,time_stamp
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'log_buffer'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'device_id,time_stamp'
           AND SUM(SUB_PART IS NOT NULL) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE log_buffer ADD INDEX ix_log_buffer_device_time_stamp (device_id, time_stamp)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

-- log_buffer: sensor_device_id,time_stamp
SET @shms_exists := (
    SELECT COUNT(*) FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'log_buffer'
        GROUP BY INDEX_NAME
        HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'sensor_device_id,time_stamp'
           AND SUM(SUB_PART IS NOT NULL) = 0
    ) existing_indexes
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE log_buffer ADD INDEX ix_log_buffer_sensor_device_time_stamp (sensor_device_id, time_stamp)',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'structural_assets'
      AND COLUMN_NAME = 'site_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'shms_sites' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE structural_assets ADD CONSTRAINT fk_structural_assets_site FOREIGN KEY (site_id) REFERENCES shms_sites (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_types'
      AND COLUMN_NAME = 'default_unit_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'measurement_units' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_types ADD CONSTRAINT fk_sensor_types_default_unit FOREIGN KEY (default_unit_id) REFERENCES measurement_units (id) ON UPDATE CASCADE ON DELETE SET NULL',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_devices'
      AND COLUMN_NAME = 'site_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'shms_sites' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_devices ADD CONSTRAINT fk_sensor_devices_site FOREIGN KEY (site_id) REFERENCES shms_sites (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_devices'
      AND COLUMN_NAME = 'asset_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'structural_assets' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_devices ADD CONSTRAINT fk_sensor_devices_asset FOREIGN KEY (asset_id) REFERENCES structural_assets (id) ON UPDATE CASCADE ON DELETE SET NULL',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_devices'
      AND COLUMN_NAME = 'zone_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'asset_zones' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_devices ADD CONSTRAINT fk_sensor_devices_zone FOREIGN KEY (zone_id) REFERENCES asset_zones (id) ON UPDATE CASCADE ON DELETE SET NULL',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_devices'
      AND COLUMN_NAME = 'sensor_type_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'sensor_types' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_devices ADD CONSTRAINT fk_sensor_devices_sensor_type FOREIGN KEY (sensor_type_id) REFERENCES sensor_types (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_channels'
      AND COLUMN_NAME = 'sensor_device_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'sensor_devices' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_channels ADD CONSTRAINT fk_sensor_channels_device FOREIGN KEY (sensor_device_id) REFERENCES sensor_devices (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_channels'
      AND COLUMN_NAME = 'unit_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'measurement_units' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_channels ADD CONSTRAINT fk_sensor_channels_unit FOREIGN KEY (unit_id) REFERENCES measurement_units (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sensor_readings'
      AND COLUMN_NAME = 'sensor_channel_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'sensor_channels' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE sensor_readings ADD CONSTRAINT fk_sensor_readings_channel FOREIGN KEY (sensor_channel_id) REFERENCES sensor_channels (id) ON UPDATE CASCADE ON DELETE RESTRICT',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (
    SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'log_buffer'
      AND COLUMN_NAME = 'sensor_device_id' AND REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'sensor_devices' AND REFERENCED_COLUMN_NAME = 'id'
);
SET @shms_sql := IF(@shms_exists = 0,
    'ALTER TABLE log_buffer ADD CONSTRAINT fk_log_buffer_sensor_device FOREIGN KEY (sensor_device_id) REFERENCES sensor_devices (id) ON UPDATE CASCADE ON DELETE SET NULL',
    'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;
