CREATE OR REPLACE FUNCTION notifications.insert_notification_log(
    _orderId uuid
)
RETURNS bigint
LANGUAGE plpgsql
VOLATILE PARALLEL UNSAFE
AS $$
DECLARE
    _row_count bigint;
BEGIN
    INSERT INTO notifications.notificationlog (
        orderchainid,
        shipmentid,
        creatorname,
        dialogid,
        transmissionid,
        operationid,
        gatewayreference,
        recipient,
        type,
        destination,
        resource,
        status,
        sent_timestamp
    )
    SELECT
        src.orderchainid,
        src.shipmentid,
        src.creatorname,
        src.dialogid,
        src.transmissionid,
        src.operationid,
        src.gatewayreference,
        src.recipient,
        src.type,
        src.destination,
        src.resource,
        src.status,
        src.sent_timestamp
    FROM (
        SELECT
            NULL::bigint AS orderchainid,
            o.alternateid AS shipmentid,
            o.creatorname AS creatorname,
            o.notificationorder->>'ResourceId' AS resource,
            NULL::uuid AS dialogid,
            NULL::text AS transmissionid,
            email.operationid AS operationid,
            NULL::text AS gatewayreference,
            COALESCE(email.recipientorgno, email.recipientnin) AS recipient,
            o.type AS type,
            email.toaddress AS destination,
            email.result::text AS status,
            email.resulttime AS sent_timestamp
        FROM notifications.emailnotifications email
        INNER JOIN notifications.orders o ON o._id = email._orderid
        WHERE o.alternateid = _orderId

        UNION ALL

        SELECT
            NULL::bigint AS orderchainid,
            o.alternateid AS shipmentid,
            o.creatorname AS creatorname,
            o.notificationorder->>'ResourceId' AS resource,
            NULL::uuid AS dialogid,
            NULL::text AS transmissionid,
            NULL::text AS operationid,
            sms.gatewayreference AS gatewayreference,
            COALESCE(sms.recipientorgno, sms.recipientnin) AS recipient,
            o.type AS type,
            sms.mobilenumber AS destination,
            sms.result::text AS status,
            sms.resulttime AS sent_timestamp
        FROM notifications.smsnotifications sms
        INNER JOIN notifications.orders o ON o._id = sms._orderid
        WHERE o.alternateid = _orderId
    ) src;

    GET DIAGNOSTICS _row_count = ROW_COUNT;
    RETURN _row_count;
END;
$$;

COMMENT ON FUNCTION notifications.insert_notification_log IS
'Inserts notification log entries derived from existing email and SMS notifications for a given shipment.
Queries emailnotifications and smsnotifications joined with orders to derive all log fields.
Required parameters: _orderId - the alternate ID of the order for which to insert log entries.
Returns: The number of inserted rows';
