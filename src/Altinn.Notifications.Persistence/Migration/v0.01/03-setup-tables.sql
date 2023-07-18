CREATE TABLE IF NOT EXISTS notifications.orders
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	alternateid UUID UNIQUE NOT NULL,
	creatorname TEXT NOT NULL,
	sendersreference TEXT NOT NULL,
	created TIMESTAMPTZ NOT NULL,
	requestedsendtime TIMESTAMPTZ NOT NULL,
	processed TIMESTAMPTZ,
	processedstatus orderprocessingstate DEFAULT 'registered',
	notificationorder JSONB NOT NULL
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.orders TO platform_notifications;


CREATE TABLE IF NOT EXISTS notifications.emailtexts
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE,
	fromaddress TEXT NOT NULL,
	subject TEXT,
	body TEXT NOT NULL,
	contenttype TEXT NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.emailtexts TO platform_notifications;
