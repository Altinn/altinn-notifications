CREATE INDEX CONCURRENTLY IF NOT EXISTS notifications_orders_retrying_idx
    ON notifications.orders
    USING btree (processed, _id)
    WHERE (processedstatus = 'Retrying'::orderprocessingstate);

DROP INDEX CONCURRENTLY IF EXISTS notifications.notifications_retrying_processed_idx;