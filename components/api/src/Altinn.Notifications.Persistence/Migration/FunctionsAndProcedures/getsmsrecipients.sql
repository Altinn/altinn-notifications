CREATE OR REPLACE FUNCTION notifications.getsmsrecipients_v2(_orderid uuid)
RETURNS TABLE(
  recipientorgno text, 
  recipientnin text,
  mobilenumber text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN
RETURN query 
	SELECT s.recipientorgno, s.recipientnin, s.mobilenumber
	FROM notifications.smsnotifications s
	WHERE s._orderid = __orderid;
END;
$BODY$;