-- MySQL 8.0+. Select the SHMS database before running.
-- Removes obsolete PLC / production settings; backup values and sensor data remain.
-- Back up before applying: dropping columns is irreversible and DDL commits implicitly.
-- Historical migrations remain unchanged. Apply this migration after older migrations.

SET @shms_drop_fks := (
    SELECT GROUP_CONCAT(DISTINCT CONCAT('DROP FOREIGN KEY `', CONSTRAINT_NAME, '`') ORDER BY CONSTRAINT_NAME SEPARATOR ', ')
    FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'system_settings'
      AND COLUMN_NAME IN ('pressure_unit_id', 'cycle_time_unit_id', 'plc_ip_address')
      AND REFERENCED_TABLE_NAME IS NOT NULL
);
SET @shms_drop_columns := (
    SELECT GROUP_CONCAT(CONCAT('DROP COLUMN `', COLUMN_NAME, '`') ORDER BY ORDINAL_POSITION SEPARATOR ', ')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'system_settings'
      AND COLUMN_NAME IN ('pressure_unit_id', 'cycle_time_unit_id', 'plc_ip_address')
);
SET @shms_drop_clauses := CONCAT_WS(', ', @shms_drop_fks, @shms_drop_columns);
SET @shms_sql := IF(LENGTH(@shms_drop_clauses) > 0,
    CONCAT('ALTER TABLE system_settings ', @shms_drop_clauses), 'SELECT 1');
PREPARE shms_stmt FROM @shms_sql;
EXECUTE shms_stmt;
DEALLOCATE PREPARE shms_stmt;
