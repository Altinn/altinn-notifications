CREATE INDEX IF NOT EXISTS notifications_emailnotifications_recipientnin ON notifications.emailnotifications (recipientnin,_orderid,_id);
CREATE INDEX IF NOT EXISTS notifications_smsnotifications_recipientnin ON notifications.smsnotifications (recipientnin,_orderid,_id);
