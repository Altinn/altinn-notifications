CREATE INDEX IF NOT EXISTS notifications_emailnotifications_expiry_succeeded_idx
  ON notifications.emailnotifications (expirytime)
  INCLUDE (_id, alternateid)
  WHERE result = 'Succeeded'::emailnotificationresulttype;

CREATE INDEX IF NOT EXISTS notifications_smsnotifications_expiry_accepted_idx
  ON notifications.smsnotifications (expirytime)
  INCLUDE (_id, alternateid)
  WHERE result = 'Accepted'::smsnotificationresulttype;
