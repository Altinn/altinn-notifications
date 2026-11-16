-- nextattempt delays the next claim after an inconclusive send condition check. A non-null value marks the notification as being on its retry.
ALTER TABLE notifications.smsnotifications ADD COLUMN IF NOT EXISTS nextattempt TIMESTAMPTZ NULL;
ALTER TABLE notifications.emailnotifications ADD COLUMN IF NOT EXISTS nextattempt TIMESTAMPTZ NULL;
