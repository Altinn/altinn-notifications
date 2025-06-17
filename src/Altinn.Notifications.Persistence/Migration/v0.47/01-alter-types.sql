DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_type t
        JOIN pg_enum e ON t.oid = e.enumtypid
        WHERE t.typname = 'emailnotificationresulttype'
          AND e.enumlabel = 'Failed_TTL'
    ) THEN
        ALTER TYPE public.emailnotificationresulttype ADD VALUE 'Failed_TTL';
    END IF;
END;
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_type t
        JOIN pg_enum e ON t.oid = e.enumtypid
        WHERE t.typname = 'smsnotificationresulttype'
          AND e.enumlabel = 'Failed_TTL'
    ) THEN
        ALTER TYPE public.smsnotificationresulttype ADD VALUE 'Failed_TTL';
    END IF;
END;
$$;
