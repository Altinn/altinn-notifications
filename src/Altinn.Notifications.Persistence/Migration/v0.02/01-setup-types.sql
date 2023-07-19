DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'emailnotificationresulttype') THEN
        CREATE TYPE emailnotificationresulttype AS ENUM ('new', 'sending', 'succeeded', 'failed_recipientnotidentified');
    END IF;
END$$;