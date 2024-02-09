CREATE INDEX IF NOT EXISTS orders_alternateid_creatorname ON notifications.orders (alternateid, creatorname);


CREATE INDEX IF NOT EXISTS notifications_smsnotifications_orderid ON notifications.smsnotifications (_orderid) include (_id, result);
CREATE INDEX IF NOT EXISTS notifications_smsnotifications_recipient ON notifications.smsnotifications (recipientid, mobilenumber) include (_id);
CREATE INDEX IF NOT EXISTS notifications_smsnotifications_result ON notifications.smsnotifications (result) include (_id, _orderid);
CREATE INDEX IF NOT EXISTS notifications_smstexts_orderid ON notifications.smstexts (_orderid);