DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 
                   FROM pg_type
                   WHERE typname = 'notificationordertypes'
                   AND typnamespace = 'public'::regnamespace) THEN
        CREATE TYPE public.notificationordertypes AS ENUM ('Notification', 'Reminder');
    END IF;
END $$;