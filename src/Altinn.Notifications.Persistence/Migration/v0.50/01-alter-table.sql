ALTER TABLE notifications.orderschain
DROP CONSTRAINT IF EXISTS orderschain_unique_idempotencyid_creatorname;

CREATE UNIQUE INDEX IF NOT EXISTS orderschain_unique_idempotency_creator_type
ON notifications.orderschain (idempotencyid, creatorname, (orderchain->>'Type'));

CREATE INDEX IF NOT EXISTS idx_orderschain_type2_lookup
ON notifications.orderschain(creatorname, idempotencyid)
WHERE orderchain->>'Type' = '0';
