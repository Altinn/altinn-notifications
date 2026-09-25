ALTER TABLE notifications.smsnotifications
    ADD COLUMN IF NOT EXISTS substitutedsender text NULL;
