CREATE TABLE IF NOT EXISTS notifications.applicationownerconfig
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	orgid TEXT UNIQUE NOT NULL,
	emailaddresses TEXT,
	smsnames TEXT
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.applicationownerconfig TO platform_notifications;
