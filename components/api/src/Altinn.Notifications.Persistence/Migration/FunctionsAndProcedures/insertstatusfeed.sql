-- FUNCTION: notifications.insertstatusfeed(bigint, text, jsonb)

-- DROP FUNCTION IF EXISTS notifications.insertstatusfeed(bigint, text, jsonb);

CREATE OR REPLACE FUNCTION notifications.insertstatusfeed(
	_orderid bigint,
	_creatorname text,
	_orderstatus jsonb)
    RETURNS void
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$
BEGIN
    -- Insert the new status into the 'statusfeed' table
    INSERT INTO notifications.statusfeed (orderid, creatorname, created, orderstatus)
    VALUES (_orderid, _creatorname, now(), _orderstatus);
END;
$BODY$;

ALTER FUNCTION notifications.insertstatusfeed(bigint, text, jsonb)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.insertstatusfeed(bigint, text, jsonb) IS
'This function inserts a new status update record into the `notifications.statusfeed` table.

Arguments:
- _orderid (bigint): The unique identifier for the order being updated.
- _creatorname (text): The name of the service owner for which  the status entry is relevant.
- _orderstatus (jsonb): A JSONB object containing the specific details of the order status at this point in time.

The function automatically records the current timestamp (`now()`) for the `created` column upon insertion. It returns void.';
