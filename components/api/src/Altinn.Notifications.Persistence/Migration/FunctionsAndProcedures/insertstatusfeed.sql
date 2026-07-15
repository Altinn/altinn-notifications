-- FUNCTION: notifications.insertstatusfeed_v2(bigint, text, jsonb)

CREATE OR REPLACE FUNCTION notifications.insertstatusfeed_v2(
	_orderid bigint,
	_creatorname text,
	_orderstatus jsonb)
    RETURNS boolean
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$
DECLARE
    _row_count bigint;
BEGIN
    -- Insert the new status into the 'statusfeed' table.
    -- statusfeed has a unique constraint on orderid, so a retried call for an order that
    -- already has an entry is a no-op instead of raising a unique-violation error.
    INSERT INTO notifications.statusfeed (orderid, creatorname, created, orderstatus)
    VALUES (_orderid, _creatorname, now(), _orderstatus)
    ON CONFLICT (orderid) DO NOTHING;

    GET DIAGNOSTICS _row_count = ROW_COUNT;
    RETURN _row_count > 0;
END;
$BODY$;

ALTER FUNCTION notifications.insertstatusfeed_v2(bigint, text, jsonb)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.insertstatusfeed_v2(bigint, text, jsonb) IS
'This function inserts a new status update record into the `notifications.statusfeed` table.

Arguments:
- _orderid (bigint): The unique identifier for the order being updated.
- _creatorname (text): The name of the service owner for which  the status entry is relevant.
- _orderstatus (jsonb): A JSONB object containing the specific details of the order status at this point in time.

The function automatically records the current timestamp (`now()`) for the `created` column upon insertion.
Idempotent: a retried call for an order that already has a status feed entry is a no-op (ON CONFLICT DO NOTHING)
instead of raising a unique-violation error.

Returns: true if a row was inserted, false if the insert was skipped because this orderid already had an entry.';
