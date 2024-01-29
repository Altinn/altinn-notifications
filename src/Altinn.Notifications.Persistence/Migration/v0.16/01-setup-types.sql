DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'smsnotificationresulttype') THEN
        CREATE TYPE smsnotificationresulttype AS ENUM ('New', 'Sending', 'Accepted', 'Failed_InvalidReceiver', 'Failed');
    END IF;
END$$;