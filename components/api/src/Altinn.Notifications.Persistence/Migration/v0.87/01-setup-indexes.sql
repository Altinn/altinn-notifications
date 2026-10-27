CREATE INDEX IF NOT EXISTS notifications_emailnotifications_recipientorgno ON notifications.emailnotifications (recipientorgno,_orderid,_id);
CREATE INDEX IF NOT EXISTS notifications_smsnotifications_recipientorgno ON notifications.smsnotifications (recipientorgno,_orderid,_id);
