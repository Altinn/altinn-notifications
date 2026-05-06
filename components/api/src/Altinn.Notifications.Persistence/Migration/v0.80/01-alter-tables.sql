ALTER TABLE notifications.orders
    ADD COLUMN IF NOT EXISTS emailsendingtimepolicy INTEGER NULL;