CREATE OR REPLACE FUNCTION notifications.getorder_pastsendtime()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
	WITH normal AS
	(
		SELECT notificationorder AS notificationorders
		FROM notifications.orders
		WHERE processedstatus = 'Registered'::orderprocessingstate AND requestedsendtime <= now() + INTERVAL '1 minute'
		ORDER BY requestedsendtime ASC, _id ASC
		LIMIT 1
		FOR UPDATE SKIP locked
	),
	retry AS
	(
		SELECT notificationorder AS notificationorders
		FROM notifications.orders
		WHERE processedstatus = 'Retrying'::orderprocessingstate AND processed <= now() - INTERVAL '1 minute'
		ORDER BY processed  ASC, _id ASC
		LIMIT 1
		FOR UPDATE SKIP locked
	)
	SELECT final.notificationorders FROM (SELECT 1 AS priority, * FROM normal UNION all SELECT 2 AS priority, * FROM retry) final
		ORDER BY priority
		LIMIT 1;
END;
$BODY$;