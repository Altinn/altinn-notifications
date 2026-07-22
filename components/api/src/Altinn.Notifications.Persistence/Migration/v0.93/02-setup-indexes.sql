CREATE INDEX IF NOT EXISTS notifications_emailnotifications_toaddress_lower ON notifications.emailnotifications (lower(toaddress),_orderid,_id);
