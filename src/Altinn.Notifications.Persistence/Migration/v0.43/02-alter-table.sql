DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM information_schema.columns 
                   WHERE column_name = 'type'
                   AND table_name   = 'orders'
                   AND table_schema = 'notifications' ) THEN
        
        ALTER TABLE notifications.orders ADD COLUMN type public.notificationordertypes 
            NOT NULL DEFAULT 'Notification'::public.notificationordertypes;
    END IF;
END $$;