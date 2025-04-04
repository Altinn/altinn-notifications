CREATE OR REPLACE PROCEDURE notifications.insertorderchain(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
LANGUAGE 'plpgsql'
AS $BODY$
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
    );
END;
$BODY$;
