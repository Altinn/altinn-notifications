CREATE TABLE IF NOT EXISTS notifications.notificationlog (
    id bigint GENERATED ALWAYS AS IDENTITY,
    orderchainid uuid,
    shipmentid uuid NOT NULL,
    notificationid uuid NOT NULL, -- The alternateid of the source email/sms notification. Idempotency key.
    creatorname text,
    dialogid text,
    transmissionid text,
    operationid text,
    gatewayreference text,
    recipient text,    -- Organization number or national identity number
    type text NOT NULL,
    channel text NOT NULL, -- 'Email' or 'Sms'
    destination text,  -- Email address or phone number
    resource text,
    status text,
    created_timestamp timestamp with time zone,
    last_update_timestamp timestamp with time zone,
    PRIMARY KEY (id, created_timestamp),
    UNIQUE (notificationid, created_timestamp)
) PARTITION BY RANGE (created_timestamp);

-- Add IF NOT EXISTS for partition tables
CREATE TABLE IF NOT EXISTS notifications.notificationlog_2026 PARTITION OF notifications.notificationlog
    FOR VALUES FROM ('2026-01-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS notifications.notificationlog_2027 PARTITION OF notifications.notificationlog
    FOR VALUES FROM ('2027-01-01 00:00:00+00') TO ('2028-01-01 00:00:00+00');

-- Add IF NOT EXISTS for default partition
CREATE TABLE IF NOT EXISTS notifications.notificationlog_default PARTITION OF notifications.notificationlog
    DEFAULT;

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.notificationlog TO platform_notifications;

-- Grant access to the auto-generated sequence for the identity column
GRANT USAGE, SELECT ON SEQUENCE notifications.notificationlog_id_seq TO platform_notifications;

CREATE INDEX IF NOT EXISTS idx_notificationlog_shipmentid ON notifications.notificationlog (shipmentid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_dialogid ON notifications.notificationlog (dialogid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_transmissionid ON notifications.notificationlog (transmissionid);
