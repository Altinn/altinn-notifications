-- To support efficient querying for metrics in smsnotifications
CREATE INDEX IF NOT EXISTS notifications_smsnotifications_resulttime ON notifications.smsnotifications (resulttime);

