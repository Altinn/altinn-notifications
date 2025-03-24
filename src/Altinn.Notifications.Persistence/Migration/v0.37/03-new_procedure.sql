CREATE OR REPLACE PROCEDURE notifications.insertorderchain(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    existing_idempotency TEXT;
BEGIN
    -- If there is an existing row for this creator, use its idempotencyid.
    SELECT idempotencyid
      INTO existing_idempotency
      FROM notifications.orderschain
     WHERE creatorname = _creatorname
     LIMIT 1;
     
    IF existing_idempotency IS NOT NULL THEN
         _idempotencyid := existing_idempotency;
    END IF;

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