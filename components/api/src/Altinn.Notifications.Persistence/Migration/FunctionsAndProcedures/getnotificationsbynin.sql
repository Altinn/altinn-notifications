CREATE OR REPLACE FUNCTION notifications.get_notifications_by_nin
(
    _recipientnin text,
    _from_date timestamptz,
    _to_date timestamptz
)
RETURNS TABLE (
    notificationid uuid,
    _orderid bigint,
    sendersreference text,
    creatorname text,
    resourceid text,
    requestedsendtime timestamptz,
    recipientorgno text,
    recipientnin text,
    channel text,
    result text,
    resulttime timestamptz
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH combined AS (
        SELECT
            e.alternateid AS notificationid,
            o._id AS _orderid,
            o.sendersreference,
            o.creatorname,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.requestedsendtime,
            e.recipientorgno,
            e.recipientnin,
            'email'::text AS channel,
            e.result::text AS result,
            e.resulttime
        FROM notifications.emailnotifications e
        JOIN notifications.orders o ON o._id = e._orderid
        WHERE e.recipientnin = _recipientnin
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date

        UNION ALL

        SELECT
            s.alternateid AS notificationid,
            o._id AS _orderid,
            o.sendersreference,
            o.creatorname,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.requestedsendtime,
            s.recipientorgno,
            s.recipientnin,
            'sms'::text AS channel,
            s.result::text AS result,
            s.resulttime
        FROM notifications.smsnotifications s
        JOIN notifications.orders o ON o._id = s._orderid
        WHERE s.recipientnin = _recipientnin
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date
    )
    SELECT * FROM combined
    ORDER BY requestedsendtime DESC;
END;
$$;

COMMENT ON FUNCTION notifications.get_notifications_by_nin IS
'Retrieves all email and SMS notifications sent to a recipient identified by their national identity number (NIN) within a given date range.
Parameters:
- _recipientnin: The national identity number of the recipient
- _from_date: Start of the date range (inclusive) based on requestedsendtime
- _to_date: End of the date range (exclusive) based on requestedsendtime
Returns a table with the following columns:
- notificationid: The unique identifier for the notification
- _orderid: The internal order ID
- creatorname: The short name of the organisation that created the order
- resourceid: The Altinn resource the notification is related to (may be null)
- sendersreference: The sender''s reference for the order
- requestedsendtime: When the notification was requested to be sent
- recipientorgno: The recipient''s organisation number (if applicable)
- recipientnin: The recipient''s national identity number
- channel: The delivery channel (''email'' or ''sms'')
- result: The delivery result status
- resulttime: When the result was recorded';
