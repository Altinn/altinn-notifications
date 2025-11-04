DROP TABLE IF EXISTS notifications.notificationlog;

CREATE TABLE IF NOT EXISTS notifications.notificationlog (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orderchainid int8,
    shipmentid uuid NOT NULL,
    dialogid uuid,
    transmissionid text,
    identifier text, 
    identifiertype text,
    recipient text,  -- Can store both orgnr and ssn
    type notificationordertype NOT NULL,
    destination text NOT NULL,  -- Email address or phone number
    resource text,
    status text,
    is_reminder boolean DEFAULT false,
    sent_timestamp timestamp with time zone
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.notificationlog TO platform_notifications;

CREATE INDEX IF NOT EXISTS idx_notificationlog_shipmentid ON notifications.notificationlog (shipmentid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_orderchainid ON notifications.notificationlog (orderchainid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_recipient ON notifications.notificationlog (recipient);
CREATE INDEX IF NOT EXISTS idx_notificationlog_sent_timestamp ON notifications.notificationlog (sent_timestamp);
