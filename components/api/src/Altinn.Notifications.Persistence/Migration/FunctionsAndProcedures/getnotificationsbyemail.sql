CREATE OR REPLACE FUNCTION notifications.get_notifications_by_email
(
    _email text,
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
    WHERE lower(e.toaddress) = lower(_email)
      AND o.requestedsendtime >= _from_date
      AND o.requestedsendtime <  _to_date
    ORDER BY o.requestedsendtime DESC;
$$;

COMMENT ON FUNCTION notifications.get_notifications_by_email IS
'Retrieves all email notifications sent to a recipient identified by their email address within a given date range.
Parameters:
- _email: The email address of the recipient (matched case-insensitively)
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
- address: The address the notification was sent to (the recipient''s email address)
- channel: The delivery channel (always ''email'' for this function)
- result: The delivery result status
- resulttime: When the result was recorded';

CREATE OR REPLACE FUNCTION notifications.get_notifications_by_email_v2
(
    _email text,
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
    resulttime timestamptz,
    dialogid text,
    senderaddress text
)
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
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
        e.resulttime,
        c.orderchain->'DialogportenAssociation'->>'DialogId' AS dialogid,
        COALESCE(et.fromaddress, st.sendernumber) AS senderaddress
    FROM notifications.emailnotifications e
    JOIN notifications.orders o ON o._id = e._orderid
    LEFT JOIN notifications.orderschain c ON c._id = o._orderchainid
    LEFT JOIN notifications.emailtexts et ON et._orderid = o._id
    LEFT JOIN notifications.smstexts st ON st._orderid = o._id
    WHERE lower(e.toaddress) = lower(_email)
      AND o.requestedsendtime >= _from_date
      AND o.requestedsendtime <  _to_date
    ORDER BY o.requestedsendtime DESC;
$$;

COMMENT ON FUNCTION notifications.get_notifications_by_email_v2 IS
'Retrieves all email notifications sent to a recipient identified by their email address within a given date range.
Parameters:
- _email: The email address of the recipient (matched case-insensitively)
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
- address: The address the notification was sent to (the recipient''s email address)
- channel: The delivery channel (always ''email'' for this function)
- result: The delivery result status
- resulttime: When the result was recorded
- dialogid: The Dialogporten dialog id the order is associated with (may be null)
- senderaddress: The email address the order was sent from, or the SMS sender number if the order has no email text (may be null)';
