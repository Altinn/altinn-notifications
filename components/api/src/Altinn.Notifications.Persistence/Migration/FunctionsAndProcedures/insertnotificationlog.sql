CREATE OR REPLACE FUNCTION notifications.insert_notification_log(
    _shipmentId uuid
)
RETURNS uuid[]
LANGUAGE plpgsql
VOLATILE PARALLEL UNSAFE
AS $$
DECLARE
    _skipped_notification_ids uuid[];
BEGIN
    WITH candidates AS (
        SELECT
            c.orderid AS orderchainid,
            o.alternateid AS shipmentid,
            email.alternateid AS notificationid,
            o.creatorname AS creatorname,
            o.notificationorder->>'ResourceId' AS resource,
            (c.orderchain->'dialogportenAssociation'->>'dialogId') AS dialogid,
            c.orderchain->'dialogportenAssociation'->>'transmissionId' AS transmissionid,
            email.operationid AS operationid,
            NULL::text AS gatewayreference,
            COALESCE(email.recipientorgno, email.recipientnin) AS recipient,
            o.type::text AS type,
            'Email'::text AS channel,
            email.toaddress AS destination,
            email.result::text AS status,
            o.created AS created_timestamp,
            email.resulttime AS last_update_timestamp
        FROM notifications.emailnotifications email
        INNER JOIN notifications.orders o ON o._id = email._orderid
        LEFT JOIN notifications.orderschain c ON c._id = o._orderchainid
        WHERE o.alternateid = _shipmentId

        UNION ALL

        SELECT
            c.orderid AS orderchainid,
            o.alternateid AS shipmentid,
            sms.alternateid AS notificationid,
            o.creatorname AS creatorname,
            o.notificationorder->>'ResourceId' AS resource,
            (c.orderchain->'dialogportenAssociation'->>'dialogId') AS dialogid,
            c.orderchain->'dialogportenAssociation'->>'transmissionId' AS transmissionid,
            NULL::text AS operationid,
            sms.gatewayreference AS gatewayreference,
            COALESCE(sms.recipientorgno, sms.recipientnin) AS recipient,
            o.type::text AS type,
            'Sms'::text AS channel,
            sms.mobilenumber AS destination,
            sms.result::text AS status,
            o.created AS created_timestamp,
            sms.resulttime AS last_update_timestamp
        FROM notifications.smsnotifications sms
        INNER JOIN notifications.orders o ON o._id = sms._orderid
        LEFT JOIN notifications.orderschain c ON c._id = o._orderchainid
        WHERE o.alternateid = _shipmentId
    ),
    inserted AS (
        INSERT INTO notifications.notificationlog (
            orderchainid,
            shipmentid,
            notificationid,
            creatorname,
            dialogid,
            transmissionid,
            operationid,
            gatewayreference,
            recipient,
            type,
            channel,
            destination,
            resource,
            status,
            created_timestamp,
            last_update_timestamp
        )
        SELECT
            orderchainid,
            shipmentid,
            notificationid,
            creatorname,
            dialogid,
            transmissionid,
            operationid,
            gatewayreference,
            recipient,
            type,
            channel,
            destination,
            resource,
            status,
            created_timestamp,
            last_update_timestamp
        FROM candidates
        ON CONFLICT (notificationid, created_timestamp) DO NOTHING
        RETURNING notificationid
    )
    SELECT array_agg(diff.notificationid)
    INTO _skipped_notification_ids
    FROM (
        SELECT notificationid FROM candidates
        EXCEPT
        SELECT notificationid FROM inserted
    ) diff;

    RETURN _skipped_notification_ids;
END;
$$;

COMMENT ON FUNCTION notifications.insert_notification_log(uuid) IS
'Inserts notification log entries derived from existing email and SMS notifications for a given shipment.
Queries emailnotifications and smsnotifications joined with orders to derive all log fields.
Idempotent: rows are keyed by the source notification''s alternateid (notificationid), so a retried call
for a shipment that was already logged inserts nothing new for those notifications (ON CONFLICT DO NOTHING).
Required parameters: _shipmentId - the alternate ID of the order for which to insert log entries.
Returns: NULL if every candidate notification was logged successfully, otherwise the uuid[] of notificationids that were skipped because a log entry already existed for them.';
