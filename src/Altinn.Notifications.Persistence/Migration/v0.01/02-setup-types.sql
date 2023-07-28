DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'orderprocessingstate') THEN
        CREATE TYPE orderprocessingstate AS ENUM ('Registered', 'Processing', 'Completed');
    END IF;
END$$;