DROP INDEX CONCURRENTLY IF EXISTS notifications.notifications_retrying_processed_idx;

CREATE INDEX CONCURRENTLY IF NOT EXISTS notifications_retrying_processed_idx
    ON notifications.orders
    USING btree (processed, _id)
    WHERE (processedstatus = 'Retrying'::orderprocessingstate);