CREATE OR REPLACE FUNCTION notifications.getorder_retry(_retry_delay interval)
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
		SELECT notificationorder AS notificationorders
		FROM notifications.orders
		WHERE processedstatus = 'Retrying'::orderprocessingstate AND processed <= now() - _retry_delay
		ORDER BY processed  ASC, _id ASC
		LIMIT 1
		FOR UPDATE SKIP LOCKED;
END;
$BODY$;