ALTER TABLE notifications.emailnotifications
    ADD COLUMN IF NOT EXISTS retrycount integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS retryreason text NULL;

ALTER TABLE notifications.smsnotifications
    ADD COLUMN IF NOT EXISTS retrycount integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS retryreason text NULL;
