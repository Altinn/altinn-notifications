CREATE TABLE IF NOT EXISTS notifications.podstate (
	id uuid PRIMARY KEY,
	created timestamptz NOT NULL,
	lastrefreshed timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS notifications.config (
	key text PRIMARY KEY,
	value text NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS config_key_case_insensitive ON notifications.config (lower(key));

GRANT SELECT, INSERT, UPDATE, DELETE ON notifications.podstate TO platform_notifications;
GRANT SELECT ON notifications.config TO platform_notifications;

COMMENT ON TABLE notifications.config IS
	'Optional non-negative TaskCount overrides, for example NotificationConfig:PastDueOrdersProcessLoopConfig:TaskCount. Deleting a key restores the static setting.';
