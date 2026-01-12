-- Add unique constraint to enforce one entry per orderid in statusfeed table
-- The design assumes each orderid only has one entry with the final status

-- Manual command: CREATE UNIQUE INDEX CONCURRENTLY statusfeed_unique_orderid_idx ON notifications.statusfeed (orderid);
CREATE UNIQUE INDEX IF NOT EXISTS statusfeed_unique_orderid_idx ON notifications.statusfeed (orderid);

-- Add constraint using the existing index
ALTER TABLE notifications.statusfeed
ADD CONSTRAINT statusfeed_unique_orderid UNIQUE USING INDEX statusfeed_unique_orderid_idx;
