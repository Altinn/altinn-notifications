CREATE OR REPLACE FUNCTION notifications.insertorderchain_idempotency(
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
    ON CONFLICT (idempotencyid, creatorname, (orderchain ->> 'Type')) DO NOTHING
    RETURNING _id INTO _chainid;

    RETURN _chainid;
END;
$BODY$;

COMMENT ON FUNCTION notifications.insertorderchain_idempotency(UUID, TEXT, TEXT, TIMESTAMP with time zone, JSONB) IS
'Inserts a new order chain row and returns its internal _id, or NULL if a row
with the same (idempotencyid, creatorname, (orderchain ->> ''Type'')) already exists.

A NULL return value means ON CONFLICT DO NOTHING fired — the chain was already
committed by a concurrent request. The caller must treat NULL as a duplicate
signal and skip all downstream order inserts, eliminating the 23505
unique-constraint violation caused by the TOCTOU race in FutureOrdersController.';
