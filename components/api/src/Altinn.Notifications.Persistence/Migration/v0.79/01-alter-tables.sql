ALTER TABLE notifications.emailnotifications
    ADD COLUMN IF NOT EXISTS deliveryreport jsonb NULL;

ALTER TABLE notifications.smsnotifications
    ADD COLUMN IF NOT EXISTS deliveryreport jsonb NULL;
