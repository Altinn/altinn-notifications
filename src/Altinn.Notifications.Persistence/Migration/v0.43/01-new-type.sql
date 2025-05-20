DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 
                   FROM pg_type
                   WHERE typname = 'notificationordertype'
                   AND typnamespace = 'public'::regnamespace) THEN
        CREATE TYPE public.notificationordertype AS ENUM ('Notification', 'Reminder');
    END IF;
END $$;