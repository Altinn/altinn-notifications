-- Partial indexes for claiming rows with result 'Retrying'.
--
-- NOTE: In environments with live data these are created manually using CONCURRENTLY
-- to avoid table locks:
--
--   CREATE INDEX CONCURRENTLY IF NOT EXISTS notifications_emailnotifications_retrying_idx
--       ON notifications.emailnotifications USING btree (resulttime, _id)
--       WHERE (result = 'Retrying'::emailnotificationresulttype);
--
--   CREATE INDEX CONCURRENTLY IF NOT EXISTS notifications_smsnotifications_retrying_idx
--       ON notifications.smsnotifications USING btree (resulttime, _id)
--       WHERE (result = 'Retrying'::smsnotificationresulttype);
--
-- This file ensures the indexes exist when provisioning new environments. On a fresh database
-- where the 'Retrying' enum value was added in the same migration run, Postgres rejects its use
-- in an index predicate (55P04). That case is skipped since the tables are empty.

DO $$
BEGIN
    CREATE INDEX IF NOT EXISTS notifications_emailnotifications_retrying_idx
        ON notifications.emailnotifications USING btree (resulttime, _id)
        WHERE (result = 'Retrying'::emailnotificationresulttype);

    CREATE INDEX IF NOT EXISTS notifications_smsnotifications_retrying_idx
        ON notifications.smsnotifications USING btree (resulttime, _id)
        WHERE (result = 'Retrying'::smsnotificationresulttype);
EXCEPTION
    WHEN SQLSTATE '55P04' THEN
        RAISE NOTICE 'Skipping Retrying partial indexes: enum value not yet committed in this migration run.';
END $$;
