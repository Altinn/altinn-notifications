CREATE TABLE IF NOT EXISTS notifications.smsnotifications
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE NOT NULL,
	alternateid UUID UNIQUE NOT NULL,
	recipientid TEXT,
	mobilenumber TEXT NOT NULL,
	result smsnotificationresulttype NOT NULL,
	gatewayreference TEXT,
	smscount INT,
	resulttime TIMESTAMPTZ NOT NULL,
	expirytime TIMESTAMPTZ NOT NULL	
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.smsnotifications TO platform_notifications;
