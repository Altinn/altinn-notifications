-- insertorder_v2.sql:
CREATE OR REPLACE PROCEDURE notifications.insertorder_v2(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _sendersreference TEXT,
    _created TIMESTAMPTZ,
    _requestedsendtime TIMESTAMPTZ,
    _orderwithreminder JSONB
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    INSERT INTO notifications.orders_v2(
        orderid,
        idempotencyid,
        creatorname,
        created,
        processed,
        orderwithreminder
    )
    VALUES (
        _orderid,
        _idempotencyid,
        _creatorname,
        _created,
        _created,
        _orderwithreminder
    );
END;
$BODY$;