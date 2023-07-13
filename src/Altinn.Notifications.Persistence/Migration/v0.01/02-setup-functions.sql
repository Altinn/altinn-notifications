
CREATE OR REPLACE FUNCTION notifications.insertorder(_alternateid UUID, _creatorname TEXT, _sendersreference TEXT, _created TIMESTAMPTZ, _sendtime TIMESTAMPTZ, _notificationorder JSONB)
RETURNS BIGINT
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, sendtime, notificationorder) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _sendtime, _notificationorder)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;