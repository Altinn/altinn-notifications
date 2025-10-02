CREATE OR REPLACE FUNCTION notifications.insertdeaddeliveryreport(
	_channel integer,
	_attemptcount integer,
	_deliveryreport jsonb,
	_resolved boolean,
	_firstseen timestamp with time zone,
	_lastattempt timestamp with time zone)
    RETURNS BIGINT
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$
DECLARE
	new_id BIGINT;
BEGIN
    -- Insert the delivery report into the dead delivery report table
    INSERT INTO notifications.deaddeliveryreports (channel, attemptcount, deliveryreport, resolved, firstseen, lastattempt)
    VALUES (_channel, _attemptcount, _deliveryreport, _resolved, _firstseen, _lastattempt)
	RETURNING id INTO new_id;

	RETURN new_id;
END;
$BODY$;

ALTER FUNCTION notifications.insertdeaddeliveryreport(integer, integer, jsonb, boolean, timestamp with time zone, timestamp with time zone)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.insertdeaddeliveryreport(integer, integer, jsonb, boolean, timestamp with time zone, timestamp with time zone)
    IS 'This function inserts a new delivery report record into the notifications.deaddeliveryreports table.

Arguments:
- _channel (integer): The unique identifier for the channel, meaning what type of delivery report to expect.
- _attemptcount (integer): The number of delivery attempts made.
- _deliveryreport (jsonb): A JSONB object containing the details of the delivery report.
- _resolved (boolean): A flag indicating whether the delivery issue has been resolved.
- _firstseen (TIMESTAMPTZ): The timestamp when the delivery issue was first detected.
- _lastattempt (TIMESTAMPTZ): The timestamp of the last delivery attempt.';
