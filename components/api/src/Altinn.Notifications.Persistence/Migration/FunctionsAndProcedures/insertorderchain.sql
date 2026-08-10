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
