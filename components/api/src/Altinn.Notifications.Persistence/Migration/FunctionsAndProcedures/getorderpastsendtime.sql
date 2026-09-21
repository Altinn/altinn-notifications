CREATE OR REPLACE FUNCTION notifications.getorder_pastsendtime()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
		SELECT notificationorder AS notificationorders
		FROM notifications.orders
		WHERE processedstatus = 'Registered'::orderprocessingstate AND requestedsendtime <= now() + INTERVAL '1 minute'
		ORDER BY requestedsendtime ASC, _id ASC
		LIMIT 1
		FOR UPDATE SKIP LOCKED;
END;
$BODY$;