CREATE TABLE IF NOT EXISTS notifications.smstexts(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE,
	sendernumber TEXT NOT NULL,
	body TEXT NOT NULL
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.smstexts TO platform_notifications;