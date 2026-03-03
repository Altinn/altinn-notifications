-- Add a unique constraint for idempotencyid and creatorname if it does not exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'orderschain_unique_idempotencyid_creatorname'
    ) THEN
        ALTER TABLE notifications.orderschain
        ADD CONSTRAINT orderschain_unique_idempotencyid_creatorname UNIQUE (idempotencyid, creatorname);
    END IF;
END $$;
