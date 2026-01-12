-- Add unique constraint to enforce one entry per orderid in statusfeed table
-- The design assumes each orderid only has one entry with the final status

-- Step 1: Create unique index
-- Manual command to run first: CREATE UNIQUE INDEX CONCURRENTLY idx_statusfeed_orderid_unique ON notifications.statusfeed (orderid);
CREATE UNIQUE INDEX IF NOT EXISTS idx_statusfeed_orderid_unique ON notifications.statusfeed (orderid);

-- Step 2: Add constraint using the existing index
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'statusfeed_unique_orderid'
    ) THEN
        ALTER TABLE notifications.statusfeed
        ADD CONSTRAINT statusfeed_unique_orderid UNIQUE USING INDEX idx_statusfeed_orderid_unique;
    END IF;
END $$;
