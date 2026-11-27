DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'notifications' AND         indexname = 'notifications_emailnotifications_retrying_idx')
    THEN
                CREATE INDEX notifications_emailnotifications_retrying_idx ON notifications.emailnotifications
            USING btree (resulttime, _id)
            WHERE (result = 'Retrying'::emailnotificationresulttype);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'notifications' AND         indexname = 'notifications_smsnotifications_retrying_idx')
    THEN
                CREATE INDEX notifications_smsnotifications_retrying_idx ON notifications.smsnotifications
            USING btree (resulttime, _id)
            WHERE (result = 'Retrying'::smsnotificationresulttype);
    END IF;
EXCEPTION
    WHEN SQLSTATE '55P04' THEN
        RAISE NOTICE 'Skipped creating Retrying indexes: the Retrying enum value was added in the same transaction.';
END
$$;
