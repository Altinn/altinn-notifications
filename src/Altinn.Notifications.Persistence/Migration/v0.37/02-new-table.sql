CREATE TABLE IF NOT EXISTS notifications.orders_v2
(
    _id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orderid UUID NOT NULL,
    idempotencyid TEXT NOT NULL,
    creatorname TEXT NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    processed TIMESTAMPTZ,
    processedstatus orderprocessingstate DEFAULT 'Registered',
    orderwithreminder JSONB NOT NULL,
    CONSTRAINT orders_v2_unique_idempotencyid_creatorname UNIQUE (idempotencyid, creatorname)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_orders_v2_orderid ON notifications.orders_v2(orderid);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.orders_v2 TO platform_notifications;
