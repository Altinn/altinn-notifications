ALTER TABLE notifications.orders
    ADD COLUMN IF NOT EXISTS emailsendingtimepolicy integer NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'notifications' AND table_name = 'orders' AND column_name = 'sendingtimepolicy')
    AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'notifications' AND table_name = 'orders' AND column_name = 'smssendingtimepolicy')
    THEN
        ALTER TABLE notifications.orders RENAME COLUMN sendingtimepolicy TO smssendingtimepolicy;
    END IF;
END $$;
