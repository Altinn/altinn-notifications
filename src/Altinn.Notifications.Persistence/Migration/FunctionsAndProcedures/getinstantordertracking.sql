CREATE OR REPLACE FUNCTION notifications.get_instant_order_tracking
(
    _creatorname text,
    _idempotencyid text
)
RETURNS TABLE (
    orders_chain_id uuid,
    shipment_id uuid,
    senders_reference text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    v_record_exists boolean;
BEGIN
    -- Check if record exists first to provide better error handling
    SELECT EXISTS (
        SELECT 1 
        FROM notifications.orderschain 
        WHERE creatorname = _creatorname
        AND idempotencyid = _idempotencyid
        AND orderchain->>'Type' = '2'
    ) INTO v_record_exists;

    IF NOT v_record_exists THEN
        -- Return empty result set with no rows
        RETURN;
    END IF;

    RETURN QUERY
    SELECT 
        oc.orderid AS orders_chain_id,
        (oc.orderchain->>'OrderId')::uuid AS shipment_id,
        oc.orderchain->>'SendersReference' AS senders_reference
    FROM 
        notifications.orderschain oc
    WHERE 
        oc.creatorname = _creatorname
        AND oc.idempotencyid = _idempotencyid
        AND oc.orderchain->>'Type' = '2';
END;
$BODY$;

COMMENT ON FUNCTION notifications.get_instant_order_tracking IS 
'Retrieves tracking information for an instant notification order using the creator''s short name and idempotency identifier.
This function provides a streamlined version of order tracking specifically for instant notifications,
enabling idempotent operations by allowing clients to retrieve previously submitted
notification information without creating duplicates.
Parameters:
- _creatorname: The short name of the creator that originally submitted the notification order
- _idempotencyid: The idempotency identifier that was defined when the order was created
Returns a table with the following columns:
- orders_chain_id: The unique identifier for the notification order in the system
- shipment_id: The unique identifier for the notification shipment
- senders_reference: The sender''s reference for the notification (may be null)';