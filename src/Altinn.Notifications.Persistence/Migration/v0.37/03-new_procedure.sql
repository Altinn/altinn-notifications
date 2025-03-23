CREATE OR REPLACE PROCEDURE notifications.insertorderchain(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _sendersreference TEXT,
    _created TIMESTAMPTZ,
    _requestedsendtime TIMESTAMPTZ,
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
        processedstatus,
        orderchain
    )
    VALUES (
        _orderid,
        _idempotencyid,
        _creatorname,
        _created,
        _created,
        'Registered',
        _orderchain
    );
END;
$BODY$;