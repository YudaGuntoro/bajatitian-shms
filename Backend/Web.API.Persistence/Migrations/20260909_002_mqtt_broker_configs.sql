CREATE TABLE IF NOT EXISTS mqtt_broker_configs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    config_name VARCHAR(100) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INT NOT NULL DEFAULT 1883,
    client_id VARCHAR(100) NOT NULL,
    username VARCHAR(100) NULL,
    password_secret_name VARCHAR(150) NULL,
    use_tls TINYINT(1) NOT NULL DEFAULT 0,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_mqtt_broker_configs_config_name (config_name),
    INDEX ix_mqtt_broker_configs_is_active (is_active)
);

INSERT INTO mqtt_broker_configs
    (config_name, host, port, client_id, use_tls, is_active)
VALUES
    ('default', 'localhost', 1883, 'SHMSClient', 0, 1)
ON DUPLICATE KEY UPDATE
    host = COALESCE(NULLIF(host, ''), VALUES(host)),
    port = IF(port BETWEEN 1 AND 65535, port, VALUES(port)),
    client_id = COALESCE(NULLIF(client_id, ''), VALUES(client_id)),
    updated_at = CURRENT_TIMESTAMP;
