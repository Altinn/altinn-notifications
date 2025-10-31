CREATE OR REPLACE FUNCTION notifications.getorders_pastsendtime_updatestatus()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
    WITH claimed_orders AS (
        SELECT _id
        FROM notifications.orders
        WHERE processedstatus = 'Registered'
          AND requestedsendtime <= now() + INTERVAL '1 minute'
        ORDER BY requestedsendtime ASC, _id ASC
        LIMIT 50
        FOR UPDATE SKIP LOCKED
    )
    UPDATE notifications.orders
    SET processedstatus = 'Processing'
    WHERE _id IN (SELECT _id FROM claimed_orders)
    RETURNING notificationorder AS notificationorders;
END;
$BODY$;

-- Add comment to document the function's purpose and behavior
COMMENT ON FUNCTION notifications.getorders_pastsendtime_updatestatus() IS
'Retrieves and updates notification orders that are ready for processing.
Selects up to 50 orders with:
- processedstatus = ''Registered''
- requestedsendtime <= current time + 1 minute grace period

Orders are processed in chronological order (oldest first) and status is updated to ''Processing''.
Uses row-level locking with SKIP LOCKED to handle concurrent executions safely - multiple 
instances can run simultaneously without conflicts, each processing different orders.

Returns: JSONB notification order data for the claimed and updated orders.';
