CREATE TABLE IF NOT EXISTS notifications.orders
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	alternateid UUID UNIQUE NOT NULL,    
	creatorname TEXT NOT NULL,
	sendersreference TEXT NOT NULL,
	created TIMESTAMPTZ NOT NULL,
	sendtime TIMESTAMPTZ NOT NULL,
	notificationorder JSONB NOT NULL
);


CREATE TABLE IF NOT EXISTS notifications.emailnotifications
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


CREATE TABLE IF NOT EXISTS notifications.emailtexts
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE,	
	fromaddress TEXT NOT NULL,
	subject TEXT,
	body TEXT NOT NULL,
	contenttype TEXT NOT NULL
);
