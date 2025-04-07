-- Retrieves tracking information for a notification order chain using the creator's short name and idempotency identifier.
CREATE OR REPLACE FUNCTION notifications.get_orders_chain_tracking
(
    _creatorname text,
    _idempotencyid text
)
RETURNS TABLE (
    orders_chain_id uuid,
    shipment_id uuid,
    senders_reference text,
    reminders jsonb
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
    ) INTO v_record_exists;
    
    IF NOT v_record_exists THEN
        -- Return empty result set with no rows
        RETURN;
    END IF;

    RETURN QUERY
    SELECT 
        orders_chain.orderid AS orders_chain_id,
        (orders_chain.orderchain->>'OrderId')::uuid AS shipment_id,
        orders_chain.orderchain->>'SendersReference' AS senders_reference,
        -- Extract only OrderId and SendersReference from each reminder
        COALESCE(
            (SELECT jsonb_agg(
                jsonb_build_object(
                    'ShipmentId', reminder->>'OrderId',
                    'SendersReference', reminder->>'SendersReference'
                )
            )
            FROM jsonb_array_elements(
                CASE 
                    WHEN jsonb_typeof(orders_chain.orderchain->'Reminders') = 'array' AND 
                         (orders_chain.orderchain->'Reminders') IS NOT NULL AND
                         (orders_chain.orderchain->'Reminders') <> 'null'::jsonb
                    THEN orders_chain.orderchain->'Reminders'
                    ELSE '[]'::jsonb
                END
            ) AS reminder),
            '[]'::jsonb
        ) AS reminders
    FROM 
        notifications.orderschain orders_chain
    WHERE 
        orders_chain.creatorname = _creatorname
        AND orders_chain.idempotencyid = _idempotencyid;
END;
$BODY$;

COMMENT ON FUNCTION notifications.get_orders_chain_tracking IS 
'Retrieves tracking information for a notification order chain using the creator''s short name and idempotency identifier.

This function enables idempotent operations by allowing clients to retrieve previously submitted
notification chain information without creating duplicates.

Parameters:
- _creatorname: The short name of the creator that originally submitted the notification order chain
- _idempotencyid: The idempotency identifier that was defined when the order chain was created

Returns a table with the following columns:
- orders_chain_id: The unique identifier for the entire notification order chain
- shipment_id: The unique identifier for the main notification order
- senders_reference: The sender''s reference for the main notification order (may be null)
- reminders: A JSON array containing tracking information for any reminder notifications

The reminders JSON array contains objects with the following structure:
- OrderId: The unique identifier for the reminder notification order
- SendersReference: The sender''s reference for the reminder notification (may be null).';
