DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_type t
        JOIN pg_enum e ON t.oid = e.enumtypid
        WHERE t.typname = 'notificationordertype' AND e.enumlabel = 'Instant'
    ) THEN
        ALTER TYPE public.notificationordertype ADD VALUE 'Instant';
    END IF;
END;
$$;