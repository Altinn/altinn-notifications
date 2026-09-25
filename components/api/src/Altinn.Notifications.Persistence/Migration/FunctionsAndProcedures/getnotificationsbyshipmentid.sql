CREATE OR REPLACE FUNCTION notifications.get_notifications_by_shipmentid
(
    _shipmentid uuid,
    _from_date timestamptz,
    _to_date timestamptz
)
RETURNS TABLE (
    shipmentid uuid,
    sendersreference text,
    creatorname text,
    notificationtype text,
    resourceid text,
    notificationchannel text,
    requestedsendtime timestamptz,
    recipientnin text,
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
            o.type::text AS notificationtype,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.notificationorder->>'NotificationChannel' AS notificationchannel,
            o.requestedsendtime,
            e.recipientnin,
            e.recipientorgno,
            e.toaddress AS address,
            'email'::text AS channel,
            e.result::text AS result,
            e.resulttime
        FROM notifications.emailnotifications e
        JOIN notifications.orders o ON o._id = e._orderid
        WHERE o.alternateid = _shipmentid
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date

        UNION ALL

        SELECT
            o.alternateid AS shipmentid,
            o.sendersreference,
            o.creatorname,
            o.type::text AS notificationtype,
            o.notificationorder->>'ResourceId' AS resourceid,
            o.notificationorder->>'NotificationChannel' AS notificationchannel,
            o.requestedsendtime,
            s.recipientnin,
            s.recipientorgno,
            s.mobilenumber AS address,
            'sms'::text AS channel,
            s.result::text AS result,
            s.resulttime
        FROM notifications.smsnotifications s
        JOIN notifications.orders o ON o._id = s._orderid
        WHERE o.alternateid = _shipmentid
          AND o.requestedsendtime >= _from_date
          AND o.requestedsendtime <  _to_date
    )
    SELECT * FROM combined
    ORDER BY requestedsendtime DESC;
$$;

COMMENT ON FUNCTION notifications.get_notifications_by_shipmentid IS
'Retrieves all email and SMS notifications belonging to a shipment identified by its shipment id (orders.alternateid) within a given date range.
Parameters:
- _shipmentid: The shipment id (orders.alternateid) to look up
- _from_date: Start of the date range (inclusive) based on requestedsendtime
- _to_date: End of the date range (exclusive) based on requestedsendtime
Returns a table with the following columns:
- shipmentid: The unique identifier for the shipment order
- sendersreference: The sender''s reference for the order
- creatorname: The short name of the organisation that created the order
- notificationtype: The type of notification that was created (e.g ''Notification'',''Reminder'')
- resourceid: The Altinn resource the notification is related to (may be null)
- notificationchannel: The requested notification channel from the order (e.g. ''EmailPreferred'', ''SmsPreferred'')
- requestedsendtime: When the notification was requested to be sent
- recipientnin: The recipient''s national identity number, if the recipient was identified by NIN (may be null)
- recipientorgno: The recipient''s organization number, if the recipient was identified by organization number (may be null)
- address: The address the notification was sent to (email address or mobile number)
- channel: The delivery channel (''email'' or ''sms'')
- result: The delivery result status
- resulttime: When the result was recorded';
