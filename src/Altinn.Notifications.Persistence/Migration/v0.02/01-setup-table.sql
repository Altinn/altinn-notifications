﻿CREATE TABLE IF NOT EXISTS notifications.emailnotifications
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	alternateid UUID UNIQUE NOT NULL,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE,
	recipientid TEXT,
	toaddress TEXT NOT NULL,
	result TEXT NOT NULL,
	resulttime TIMESTAMPTZ NOT NULL,
	expirytime TIMESTAMPTZ NOT NULL
);
