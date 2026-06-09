CREATE INDEX IF NOT EXISTS notifications_sendersreference_creatorname ON notifications.orders (sendersreference, creatorname);
CREATE INDEX IF NOT EXISTS notifications_emailnotifications_orderid ON notifications.emailnotifications (_orderid) include (_id, result);
CREATE INDEX IF NOT EXISTS notifications_emailnotifications_result ON notifications.emailnotifications (result) include (_id, _orderid);
CREATE INDEX IF NOT EXISTS notifications_emailtexts_orderid ON notifications.emailtexts (_orderid);
