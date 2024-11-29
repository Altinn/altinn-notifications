CREATE OR REPLACE FUNCTION notifications.getemailrecipients_v2(_alternateid uuid)
RETURNS TABLE(
    recipientorgno text, 
    recipientnin text, 
    toaddress text,
	customizedbody text,
	customizedsubject text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _alternateid);
BEGIN
RETURN query 
	SELECT e.recipientorgno, e.recipientnin, e.toaddress, e.customizedbody, e.customizedsubject
	FROM notifications.emailnotifications e
	WHERE e._orderid = __orderid;
END;
$BODY$;