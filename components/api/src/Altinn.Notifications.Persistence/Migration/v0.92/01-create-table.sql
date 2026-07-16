CREATE TABLE IF NOT EXISTS notifications.notificationlog (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orderchainid uuid,
    shipmentid uuid NOT NULL,
    notificationid uuid NOT NULL UNIQUE, -- The alternateid of the source email/sms notification. Idempotency key.
    creatorname text NOT NULL,
    sendersreference text,
    dialogid text,
    transmissionid text,
    deliveryreference text, -- The email/sms provider's tracking reference for this send attempt
    recipient text,    -- Organization number or national identity number
    type text NOT NULL,
    channel text NOT NULL, -- 'Email' or 'Sms'
    destination text NOT NULL,  -- Email address or phone number
    resource text,
    status text NOT NULL,
    requestedsendtime timestamp with time zone NOT NULL,
    lastupdatetime timestamp with time zone NOT NULL
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.notificationlog TO platform_notifications;

-- Grant access to the auto-generated sequence for the identity column
GRANT USAGE, SELECT ON SEQUENCE notifications.notificationlog_id_seq TO platform_notifications;

CREATE INDEX IF NOT EXISTS idx_notificationlog_shipmentid ON notifications.notificationlog (shipmentid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_dialogid ON notifications.notificationlog (dialogid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_transmissionid ON notifications.notificationlog (transmissionid);
