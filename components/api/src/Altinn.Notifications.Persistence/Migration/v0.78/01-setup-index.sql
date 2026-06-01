-- To support efficient querying for metrics in emailnotifications
CREATE INDEX IF NOT EXISTS notifications_emailnotifications_resulttime ON notifications.emailnotifications (resulttime);

