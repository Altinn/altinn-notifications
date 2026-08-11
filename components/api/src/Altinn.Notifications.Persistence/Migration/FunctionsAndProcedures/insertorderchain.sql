CREATE OR REPLACE FUNCTION notifications.insertorderchain_v2(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
RETURNS BIGINT
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _chainid BIGINT;
BEGIN
    INSERT INTO notifications.orderschain(
        orderid,
        idempotencyid,
        creatorname,
        created,
        processed,
        orderchain
    )
    VALUES (
        _orderid,
        _idempotencyid,
        _creatorname,
        _created,
        _created,
        _orderchain
    )
    RETURNING _id INTO _chainid;

    RETURN _chainid;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.insertorderchain_v3(
    _orderchainid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
RETURNS TABLE (
    is_newly_created BOOLEAN,
    internal_id BIGINT,
    order_chain_id UUID,
    shipment_id UUID,
    senders_reference TEXT,
    reminders JSONB
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _internalid BIGINT;
BEGIN
    INSERT INTO notifications.orderschain(
        orderid,
        idempotencyid,
        creatorname,
        created,
        processed,
        orderchain
    )
    VALUES (
        _orderchainid,
        _idempotencyid,
        _creatorname,
        _created,
        _created,
        _orderchain
    )
    ON CONFLICT (idempotencyid, creatorname, (orderchain ->> 'Type')) DO NOTHING
    RETURNING _id INTO _internalid;

    IF _internalid IS NOT NULL THEN
        -- Successful insert: return data from the parameters
        RETURN QUERY
        SELECT
            TRUE,
            _internalid,
            _orderchainid,
            (_orderchain->>'OrderId')::uuid,
            _orderchain->>'SendersReference',
            '[]'::jsonb;
    ELSE
        -- Conflict: return data from the existing row
        RETURN QUERY
        SELECT
            FALSE,
            oc._id,
            oc.orderid,
            (oc.orderchain->>'OrderId')::uuid,
            oc.orderchain->>'SendersReference',
            CASE
                WHEN jsonb_typeof(oc.orderchain->'Reminders') = 'array'
                     AND oc.orderchain->'Reminders' <> 'null'::jsonb
                THEN (
                    SELECT COALESCE(jsonb_agg(
                        jsonb_build_object(
                            'ShipmentId', r->>'OrderId',
                            'SendersReference', r->>'SendersReference'
                        )
                    ), '[]'::jsonb)
                    FROM jsonb_array_elements(oc.orderchain->'Reminders') AS r
                )
                ELSE '[]'::jsonb
            END
        FROM notifications.orderschain oc
        WHERE oc.idempotencyid = _idempotencyid
          AND oc.creatorname = _creatorname
          AND oc.orderchain ->> 'Type' = _orderchain ->> 'Type';
    END IF;
END;
$BODY$;

COMMENT ON FUNCTION notifications.insertorderchain_v3(UUID, TEXT, TEXT, TIMESTAMP with time zone, JSONB) IS
'Atomically inserts a new order chain row or detects a conflict with an existing row.

Returns a single row with:
- is_newly_created: TRUE if the row was newly inserted, FALSE if a conflict was detected.
- internal_id: The internal _id of the chain row (new or existing).
- order_chain_id: The order chain UUID (orderid column).
- shipment_id: The main notification order UUID (orderchain->>''OrderId'').
- senders_reference: The sender''s reference (orderchain->>''SendersReference'').
- reminders: A JSONB array of {ShipmentId, SendersReference} for each reminder, or ''[]''.

On conflict, the existing row''s data is returned so the caller can build the
idempotent response without a separate query.';
