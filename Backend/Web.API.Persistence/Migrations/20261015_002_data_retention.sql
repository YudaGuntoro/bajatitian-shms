-- Select the existing SHMS database before running. MySQL 8.0+.
-- Repeatable, additive migration. Old rows retain NULL upload acknowledgement and
-- remain protected. Never infer delivery success from message text or age.
SET @shms_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sensor_readings' AND COLUMN_NAME='uploaded_at');
SET @shms_sql := IF(@shms_exists=0,
    'ALTER TABLE sensor_readings ADD COLUMN uploaded_at DATETIME(6) NULL AFTER ingested_at', 'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (SELECT COUNT(*) FROM (
    SELECT INDEX_NAME FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sensor_readings'
    GROUP BY INDEX_NAME HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='ingested_at,id,uploaded_at'
) existing_indexes);
SET @shms_sql := IF(@shms_exists=0, 'ALTER TABLE sensor_readings ADD INDEX ix_sensor_readings_retention (ingested_at,id,uploaded_at)', 'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

SET @shms_exists := (SELECT COUNT(*) FROM (
    SELECT INDEX_NAME FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='log_buffer'
    GROUP BY INDEX_NAME HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='status,uploaded_at'
) existing_indexes);
SET @shms_sql := IF(@shms_exists=0, 'ALTER TABLE log_buffer ADD INDEX ix_log_buffer_uploaded_retention (status,uploaded_at)', 'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;

CREATE TABLE IF NOT EXISTS data_retention_settings (
    id INT PRIMARY KEY,
    enabled TINYINT(1) NOT NULL DEFAULT 0,
    reading_retention_days INT NOT NULL DEFAULT 30,
    uploaded_log_retention_days INT NOT NULL DEFAULT 7,
    cleanup_interval_minutes INT NOT NULL DEFAULT 60,
    max_reading_records BIGINT NULL,
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT ck_retention_days CHECK (reading_retention_days BETWEEN 1 AND 3650 AND uploaded_log_retention_days BETWEEN 1 AND 3650),
    CONSTRAINT ck_retention_interval CHECK (cleanup_interval_minutes BETWEEN 1 AND 1440),
    CONSTRAINT ck_retention_capacity CHECK (max_reading_records IS NULL OR max_reading_records BETWEEN 100 AND 100000000),
    CONSTRAINT ck_retention_enabled CHECK (enabled IN (0, 1)),
    CONSTRAINT ck_retention_singleton CHECK (id = 1)
) ENGINE=InnoDB;
INSERT INTO data_retention_settings (id) VALUES (1) ON DUPLICATE KEY UPDATE id = id;

CREATE TABLE IF NOT EXISTS data_retention_status (
    id INT PRIMARY KEY,
    last_attempt_at DATETIME(6) NULL,
    last_cleanup_at DATETIME(6) NULL,
    deleted_readings BIGINT NOT NULL DEFAULT 0,
    deleted_logs BIGINT NOT NULL DEFAULT 0,
    work_pending TINYINT(1) NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    CONSTRAINT ck_retention_status_singleton CHECK (id = 1)
) ENGINE=InnoDB;
INSERT INTO data_retention_status (id) VALUES (1) ON DUPLICATE KEY UPDATE id = id;

SET @shms_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='data_retention_status' AND COLUMN_NAME='work_pending');
SET @shms_sql := IF(@shms_exists=0,
    'ALTER TABLE data_retention_status ADD COLUMN work_pending TINYINT(1) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;
