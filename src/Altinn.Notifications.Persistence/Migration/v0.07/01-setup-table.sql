CREATE TABLE IF NOT EXISTS notifications.applicationownerconfig
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	orgid TEXT UNIQUE NOT NULL,
	fromaddresses TEXT
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.applicationownerconfig TO platform_notifications;
