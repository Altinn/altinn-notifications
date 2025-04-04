-- Add a unique constraint for idempotencyid and creatorname
ALTER TABLE notifications.orderschain
ADD CONSTRAINT orderschain_unique_idempotencyid_creatorname UNIQUE (idempotencyid, creatorname);
