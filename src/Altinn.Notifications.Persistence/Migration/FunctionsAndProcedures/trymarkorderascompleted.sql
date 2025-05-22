CREATE OR REPLACE FUNCTION notifications.trymarkorderascompleted(_alternateid uuid, _alternateidsource text)
RETURNS boolean AS $$
DECLARE
    order_id bigint;
    order_status orderprocessingstate;
    has_pending_notifications boolean := false;
BEGIN
    IF _alternateid IS NULL THEN
        RAISE EXCEPTION 'Notification ID cannot be null';
    END IF;

    IF _alternateidsource IS NULL OR LENGTH(TRIM(_alternateidsource)) = 0 THEN
        RAISE EXCEPTION 'Notification type cannot be null or empty';
    END IF;

    -- Convert notification type to uppercase for case-insensitive comparison
    _alternateidsource := UPPER(TRIM(_alternateidsource));

    -- Step 2: Find the order ID based on notification type
    CASE _alternateidsource
        WHEN 'SMS' THEN
            SELECT _orderid INTO order_id 
            FROM notifications.smsnotifications 
            WHERE alternateid = _alternateid
            LIMIT 1;

        WHEN 'EMAIL' THEN
            SELECT _orderid INTO order_id 
            FROM notifications.emailnotifications 
            WHERE alternateid = _alternateid
            LIMIT 1;

        WHEN 'ORDER' THEN
            SELECT _id INTO order_id
            FROM notifications.orders
            WHERE alternateid = _alternateid
            LIMIT 1;

        ELSE
            RAISE EXCEPTION 'Invalid notification type: %. Must be one of: SMS, EMAIL, ORDER', _alternateidsource;
    END CASE;

    -- Step 3: Validate order ID exists
    IF order_id IS NULL THEN
        RAISE EXCEPTION 'No order found for notification ID % with source type %', _alternateid, _alternateidsource;
    END IF;

    -- Step 4: Check if order is already completed (with row lock)
    SELECT processedstatus INTO order_status
    FROM notifications.orders
    WHERE _id = order_id
    FOR UPDATE;

    IF order_status IS NULL OR order_status = 'Completed'::orderprocessingstate THEN
        RETURN false;
    END IF;

    -- Step 5: Check if any notifications are still pending
    WITH pending_notifications AS (    WITH pending_notifications AS (
        SELECT 1 AS is_pending
        FROM notifications.smsnotifications 
        WHERE _orderid = order_id 
        AND result IN ('New'::smsnotificationresulttype, 'Sending'::smsnotificationresulttype, 'Accepted'::smsnotificationresulttype)

        UNION ALL

        SELECT 1 AS is_pending
        FROM notifications.emailnotifications 
        WHERE _orderid = order_id 
        AND result::TEXT IN ('New'::emailnotificationresulttype, 'Sending'::emailnotificationresulttype, 'Succeeded'::emailnotificationresulttype)
    )
    SELECT EXISTS(SELECT 1 FROM pending_notifications) INTO has_pending_notifications;

    -- Step 6: Update order status based on notification states
    UPDATE notifications.orders
    SET processedstatus = CASE 
                            WHEN has_pending_notifications THEN 'Processed'
                            ELSE 'Completed'
                          END::orderprocessingstate,
        processed = CURRENT_TIMESTAMP
    WHERE _id = order_id
    AND processedstatus IS DISTINCT FROM (CASE WHEN has_pending_notifications THEN 'Processed' ELSE 'Completed' END::orderprocessingstate   );

    RETURN NOT has_pending_notifications;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION notifications.trymarkorderascompleted IS 
'Attempts to mark a notification order as completed based on the status
of its associated SMS and Email notifications. The function updates the order''s
status to ''Completed'' only if all associated notifications are no longer pending.

Parameters:
  _alternateid uuid       - The UUID identifier for the SMS, Email notifications or order
  _alternateidsource text - The source type, must be one of: ''SMS'', ''EMAIL'', or ''ORDER'' (case-insensitive)

Returns:
  boolean - TRUE if the order was successfully marked as completed
          FALSE if the order cannot be completed (already completed or has pending notifications)

Side Effects:
  - Updates the processedstatus and processed timestamp in notifications.orders table
  - Sets order status to ''Completed'' when no pending notifications exist
  - Sets order status to ''Processed'' when pending notifications still exist

Throws:
  - Exception if _alternateid is NULL
  - Exception if _alternateidsource is NULL or empty
  - Exception if _alternateidsource is not one of: ''SMS'', ''EMAIL'', ''ORDER''
  - Exception if no order is found for the given notification ID and source';