-- Retrieves tracking information for a composed email order chain using the creator's short name and idempotency identifier.
CREATE OR REPLACE FUNCTION notifications.get_composed_order_chain_tracking
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
STABLE
AS $BODY$
    SELECT
        orders_chain.orderid AS orders_chain_id,
        (orders_chain.orderchain->>'OrderId')::uuid AS shipment_id,
        orders_chain.orderchain->>'SendersReference' AS senders_reference
    FROM
        notifications.orderschain orders_chain
    WHERE
        orders_chain.creatorname = _creatorname
        AND orders_chain.idempotencyid = _idempotencyid
        -- include only type 'Composed' (3)
        AND orders_chain.orderchain->>'Type' = '3';
$BODY$;

COMMENT ON FUNCTION notifications.get_composed_order_chain_tracking IS
'Retrieves tracking information for a composed email order chain using the creator''s short name and idempotency identifier.

This function is scoped exclusively to composed email orders (OrderType = 3) to ensure idempotency
identifiers are isolated per order type. Composed email orders do not support reminders, so no
reminders column is returned. Use get_orders_chain_tracking_v2 for standard notification orders
and get_instant_order_tracking for instant orders.

Parameters:
- _creatorname: The short name of the creator that originally submitted the composed email order chain
- _idempotencyid: The idempotency identifier that was defined when the order chain was created

Returns a table with the following columns:
- orders_chain_id: The unique identifier for the entire order chain
- shipment_id: The unique identifier for the main notification order
- senders_reference: The sender''s reference for the main notification order (may be null).';
