CREATE INDEX IF NOT EXISTS email_expiry_succeeded_idx
  ON notifications.emailnotifications (expirytime, _id)
  WHERE result = 'Succeeded';

CREATE INDEX IF NOT EXISTS sms_expiry_accepted_idx
  ON notifications.smsnotifications (expirytime, _id)
  WHERE result = 'Accepted';
