-- Retrieves notification order chain shipment data by creator name and idempotency identifier.
CREATE OR REPLACE FUNCTION notifications.get_orders_chain_shipments
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

-- Add a comment to the function
COMMENT ON FUNCTION notifications.get_orders_chain_shipments IS 
'Retrieves notification order chain shipment data by creator name and idempotency identifier.

Parameters:
- _creatorname: The name of the creator
- _idempotencyid: The idempotency identifier

Returns:
- orderid: The unique identifier for the order chain
- shipment_id: The ID of the main notification order
- senders_reference: The sender reference for the main order (may be null)
- reminders: JSON array of reminder shipments

The reminders field contains a JSON array where each object has:
- shipment_id: UUID of the reminder notification
- senders_reference: Optional reference for the reminder notification';