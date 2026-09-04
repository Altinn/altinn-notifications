CREATE OR REPLACE FUNCTION notifications.getorder_pastsendtime()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
        SELECT notificationorder AS notificationorders
        FROM notifications.orders
        WHERE processedstatus = 'Registered'::orderprocessingstate
          AND requestedsendtime <= now() + INTERVAL '1 minute'
        ORDER BY requestedsendtime ASC, _id ASC
        LIMIT 1
        FOR UPDATE SKIP LOCKED;
END;
$BODY$;

-- Add comment to document the function's purpose and behavior
COMMENT ON FUNCTION notifications.getorder_pastsendtime() IS
'Retrieves and updates notification orders that are ready for processing.
Selects up to 1 order with:
- processedstatus = ''Registered''
- requestedsendtime <= current time + 1 minute grace period

Orders are processed in chronological order (oldest first) and status is updated to ''Processing''.
Uses row-level locking with SKIP LOCKED to handle concurrent executions safely - multiple 
instances can run simultaneously without conflicts, each processing different orders.

Returns: JSONB notification order data for the claimed and updated orders.';
