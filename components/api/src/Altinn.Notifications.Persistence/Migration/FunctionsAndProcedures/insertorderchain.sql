CREATE OR REPLACE FUNCTION notifications.insertorderchain_v3(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
RETURNS TABLE(chainid BIGINT, was_inserted BOOLEAN)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _chainid BIGINT;
    _was_inserted BOOLEAN;
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
    ON CONFLICT (idempotencyid, creatorname, type) DO NOTHING
    RETURNING _id INTO _chainid;

    IF _chainid IS NULL THEN
        SELECT _id INTO _chainid
        FROM notifications.orderschain
        WHERE idempotencyid = _idempotencyid
          AND creatorname = _creatorname;

        _was_inserted := FALSE;
    ELSE
        _was_inserted := TRUE;
    END IF;

    RETURN QUERY SELECT _chainid, _was_inserted;
END;
$BODY$;

COMMENT ON FUNCTION notifications.insertorderchain_v3(UUID, TEXT, TEXT, TIMESTAMP with time zone, JSONB) IS
'Inserts a new order chain row and returns its internal ID together with a flag
indicating whether the row was newly created.

If a row with the same (idempotencyid, creatorname, type) already exists the
INSERT is silently skipped (ON CONFLICT DO NOTHING). The existing row''s _id is
then retrieved via a fallback SELECT and was_inserted is returned as FALSE.
This makes the function safe to call concurrently with the same idempotency key,
eliminating the 23505 unique-constraint violation that would otherwise occur in
a TOCTOU race between the pre-check in FutureOrdersController and the insert.';