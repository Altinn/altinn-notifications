CREATE TABLE IF NOT EXISTS notifications.notificationlog (
    id bigint GENERATED ALWAYS AS IDENTITY,
    orderchainid int8,
    shipmentid uuid NOT NULL,
    dialogid uuid,
    transmissionid text,
    operationid text, 
    gatewayreference text,
    recipient text,    -- Can store both orgnr and ssn
    type notificationordertype NOT NULL,
    destination text,  -- Email address or phone number
    resource text,
    status text,
    created_timestamp timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    sent_timestamp timestamp with time zone,
    PRIMARY KEY (id, created_timestamp)
) PARTITION BY RANGE (created_timestamp);

-- Add IF NOT EXISTS for partition tables
CREATE TABLE IF NOT EXISTS notifications.notificationlog_2025 PARTITION OF notifications.notificationlog
    FOR VALUES FROM ('2025-01-01 00:00:00+00') TO ('2026-01-01 00:00:00+00');

-- Add IF NOT EXISTS for default partition
CREATE TABLE IF NOT EXISTS notifications.notificationlog_default PARTITION OF notifications.notificationlog
    DEFAULT;

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.notificationlog TO platform_notifications;

-- Grant access to the auto-generated sequence for the identity column
GRANT USAGE, SELECT ON SEQUENCE notifications.notificationlog_id_seq TO platform_notifications;

CREATE INDEX IF NOT EXISTS idx_notificationlog_shipmentid ON notifications.notificationlog (shipmentid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_orderchainid ON notifications.notificationlog (orderchainid);
CREATE INDEX IF NOT EXISTS idx_notificationlog_recipient ON notifications.notificationlog (recipient);
CREATE INDEX IF NOT EXISTS idx_notificationlog_sent_timestamp ON notifications.notificationlog (sent_timestamp);
