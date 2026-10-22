-- NOTE: This script has been applied manually in all environments including production

CREATE INDEX IF NOT EXISTS notifications_orders_retrying_idx
    ON notifications.orders
    USING btree (processed, _id)
    WHERE (processedstatus = 'Retrying'::orderprocessingstate);

DROP INDEX IF EXISTS notifications.notifications_retrying_processed_idx;