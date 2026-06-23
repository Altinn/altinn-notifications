CREATE OR REPLACE FUNCTION notifications.get_notifications_by_organization_number
(
    _recipientorgno text,
    _from_date timestamptz,
    _to_date timestamptz
)
RETURNS TABLE (
    shipmentid uuid,
    sendersreference text,
    creatorname text,
    resourceid text,
    notificationchannel text,
    requestedsendtime timestamptz,
    recipientorgno text,
    address text,
    channel text,
    result text,
    resulttime timestamptz
)
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
    WITH combined AS (
        SELECT
            o.alternateid AS shipmentid,
            o.sendersreference,
            o.creatorname,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.notificationorder->>'NotificationChannel' AS notificationchannel,
            o.requestedsendtime,
            e.recipientorgno,
            e.toaddress AS address,
            'email'::text AS channel,
            e.result::text AS result,
            e.resulttime
        FROM notifications.emailnotifications e
        JOIN notifications.orders o ON o._id = e._orderid
        WHERE e.recipientorgno = _recipientorgno
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date

        UNION ALL

        SELECT
            o.alternateid AS shipmentid,
            o.sendersreference,
            o.creatorname,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.notificationorder->>'NotificationChannel' AS notificationchannel,
            o.requestedsendtime,
            s.recipientorgno,
            s.mobilenumber AS address,
            'sms'::text AS channel,
            s.result::text AS result,
            s.resulttime
        FROM notifications.smsnotifications s
        JOIN notifications.orders o ON o._id = s._orderid
        WHERE s.recipientorgno = _recipientorgno
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date
    )
    SELECT * FROM combined
    ORDER BY requestedsendtime DESC;
$$;

COMMENT ON FUNCTION notifications.get_notifications_by_organization_number IS
'Retrieves all email and SMS notifications sent to a recipient identified by their organization number within a given date range.
Parameters:
- _recipientorgno: The organization number of the recipient
- _from_date: Start of the date range (inclusive) based on requestedsendtime
- _to_date: End of the date range (exclusive) based on requestedsendtime
Returns a table with the following columns:
- shipmentid: The unique identifier for the shipment order
- sendersreference: The sender''s reference for the order
- creatorname: The short name of the organisation that created the order
- resourceid: The Altinn resource the notification is related to (may be null)
- notificationchannel: The requested notification channel from the order (e.g. ''EmailPreferred'', ''SmsPreferred'')
- requestedsendtime: When the notification was requested to be sent
- recipientorgno: The recipient''s organization number
- address: The address the notification was sent to (email address or mobile number)
- channel: The delivery channel (''email'' or ''sms'')
- result: The delivery result status
- resulttime: When the result was recorded';
