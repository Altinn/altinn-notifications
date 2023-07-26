DROP FUNCTION IF EXISTS notifications.getorders_pastsendtime_updatestatus();
CREATE OR REPLACE FUNCTION notifications.getorders_pastsendtime_updatestatus()
    RETURNS TABLE(notificationorders text)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN QUERY
	UPDATE notifications.orders
	SET processedstatus = 'processing'
	WHERE _id IN (select _id 
				 from notifications.orders 
				 where processedstatus = 'registered' 
				 and requestedsendtime <= now()
				 limit 50)
	RETURNING cast(notificationorder as text) AS notificationorders;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getemails_statusnew_updatestatus()
RETURNS TABLE(
    id bigint, 
    subject text,
	body text,
	fromaddress text,
	toaddress text,
	contenttype text
) 
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN query 
	WITH updated AS (
		UPDATE notifications.emailnotifications
			SET result = 'sending'
			WHERE result = 'new' 
			RETURNING _id, _orderid, notifications.emailnotifications.toaddress)
	SELECT u._id, et.subject, et.body, et.fromaddress, u.toaddress, et.contenttype 
	FROM updated u, notifications.emailtexts et
	WHERE u._orderid = et._orderid;	
END;
$BODY$;