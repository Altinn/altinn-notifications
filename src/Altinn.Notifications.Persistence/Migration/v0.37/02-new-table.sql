CREATE TABLE IF NOT EXISTS notifications.orderschain
(
    _id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orderid UUID NOT NULL,
    idempotencyid TEXT NOT NULL,
    creatorname TEXT NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    processed TIMESTAMPTZ,
    processedstatus orderprocessingstate DEFAULT 'Registered',
    orderchain JSONB NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_orderschain_orderid ON notifications.orderschain(orderid);
GRANT USAGE, SELECT ON SEQUENCE notifications.statusfeed__id_seq TO platform_notifications;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.orderschain TO platform_notifications;
