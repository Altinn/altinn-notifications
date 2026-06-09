CREATE OR REPLACE FUNCTION notifications.getsmsrecipients(_orderid uuid)
RETURNS TABLE(
    recipientid text, 
    mobilenumber text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN
RETURN query 
	SELECT s.recipientid, s.mobilenumber
	FROM notifications.smsnotifications s
	WHERE s._orderid = __orderid;
END;
$BODY$;