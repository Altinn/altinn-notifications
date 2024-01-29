DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'smsnotificationresulttype') THEN
        CREATE TYPE smsnotificationresulttype AS ENUM ('New', 'Sending', 'Succeeded', 'Failed_InvalidReceiver');
    END IF;
END$$;