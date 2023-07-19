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