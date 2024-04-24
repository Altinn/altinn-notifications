CREATE OR REPLACE FUNCTION notifications.getorders_pastsendtime_updatestatus()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN QUERY
	UPDATE notifications.orders
	SET processedstatus = 'Processing'
	WHERE _id IN (select _id
				 from notifications.orders
				 where processedstatus = 'Registered'
				 and requestedsendtime <= now() + INTERVAL '1 minute'
				 limit 50)
	RETURNING notificationorder AS notificationorders;
END;
$BODY$;