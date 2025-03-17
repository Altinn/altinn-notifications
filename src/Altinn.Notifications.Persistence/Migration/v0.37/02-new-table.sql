CREATE TABLE IF NOT EXISTS notifications.orderswithreminders
(
    _id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    orderid UUID NOT NULL,
    idempotencyid TEXT NOT NULL,
    creatorname TEXT NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    processed TIMESTAMPTZ,
    processedstatus orderprocessingstate DEFAULT 'Registered',
    orderwithreminder JSONB NOT NULL,
    CONSTRAINT orderswithreminders_unique_idempotencyid_creatorname UNIQUE (idempotencyid, creatorname)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_orderswithreminders_orderid ON notifications.orderswithreminders(orderid);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.orderswithreminders TO platform_notifications;
