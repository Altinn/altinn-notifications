ALTER TABLE notifications.emailnotifications
    ADD COLUMN IF NOT EXISTS deliveryreport jsonb NULL;
