CREATE OR REPLACE FUNCTION notifications.insert_notification_log_v2(
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
            o.sendersreference AS sendersreference,
            c.orderchain->'DialogportenAssociation'->>'DialogId' AS dialogid,
            c.orderchain->'DialogportenAssociation'->>'TransmissionId' AS transmissionid,
            email.operationid AS deliveryreference,
            COALESCE(email.recipientorgno, email.recipientnin) AS recipient,
            o.type::text AS type,
            'Email'::text AS channel,
            email.toaddress AS destination,
            o.notificationorder->>'ResourceId' AS resource,
            email.result::text AS status,
            o.requestedsendtime AS requestedsendtime,
            email.resulttime AS lastupdatetime
        FROM notifications.emailnotifications email
        INNER JOIN notifications.orders o ON o._id = email._orderid
        LEFT JOIN notifications.orderschain c ON c._id = o._orderchainid
        WHERE o.alternateid = _shipmentId
          AND email.result NOT IN ('New'::emailnotificationresulttype, 'Sending'::emailnotificationresulttype, 'Succeeded'::emailnotificationresulttype, 'Failed_TransientError'::emailnotificationresulttype)

        UNION ALL

        SELECT
            c.orderid AS orderchainid,
            o.alternateid AS shipmentid,
            sms.alternateid AS notificationid,
            o.creatorname AS creatorname,
            o.sendersreference AS sendersreference,
            c.orderchain->'DialogportenAssociation'->>'DialogId' AS dialogid,
            c.orderchain->'DialogportenAssociation'->>'TransmissionId' AS transmissionid,
            sms.gatewayreference AS deliveryreference,
            COALESCE(sms.recipientorgno, sms.recipientnin) AS recipient,
            o.type::text AS type,
            'Sms'::text AS channel,
            sms.mobilenumber AS destination,
            o.notificationorder->>'ResourceId' AS resource,
            sms.result::text AS status,
            o.requestedsendtime AS requestedsendtime,
            sms.resulttime AS lastupdatetime
        FROM notifications.smsnotifications sms
        INNER JOIN notifications.orders o ON o._id = sms._orderid
        LEFT JOIN notifications.orderschain c ON c._id = o._orderchainid
        WHERE o.alternateid = _shipmentId
          AND sms.result NOT IN ('New'::smsnotificationresulttype, 'Sending'::smsnotificationresulttype, 'Accepted'::smsnotificationresulttype)
    ),
    inserted AS (
        INSERT INTO notifications.notificationlog (
            orderchainid,
            shipmentid,
            notificationid,
            creatorname,
            sendersreference,
            dialogid,
            transmissionid,
            deliveryreference,
            recipient,
            type,
            channel,
            destination,
            resource,
            status,
            requestedsendtime,
            lastupdatetime
        )
        SELECT
            orderchainid,
            shipmentid,
            notificationid,
            creatorname,
            sendersreference,
            dialogid,
            transmissionid,
            deliveryreference,
            recipient,
            type,
            channel,
            destination,
            resource,
            status,
            requestedsendtime,
            lastupdatetime
        FROM candidates c
        WHERE NOT EXISTS (SELECT 1 FROM notifications.notificationlog nl WHERE nl.notificationid = c.notificationid)
        ON CONFLICT (notificationid) DO NOTHING
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

COMMENT ON FUNCTION notifications.insert_notification_log_v2(uuid) IS
'Inserts notification log entries derived from the final-result email and SMS notifications for a given shipment.
Queries emailnotifications and smsnotifications joined with orders to derive all log fields.
Only notifications with a final result are logged. Pending notifications (email: New, Sending, Succeeded, Failed_TransientError; sms: New, Sending, Accepted) are ignored,
so they can be logged by a later call once they reach a final result, independently of the other notifications in the shipment.
Idempotent: rows are keyed by the source notification''s alternateid (notificationid). Notifications that are already logged are filtered out before the insert,
so a retried call writes no row and consumes no identity value for them, and reports them as skipped.
Required parameters: _shipmentId - the alternate ID of the order for which to insert log entries.
Returns: NULL if every final-result candidate notification was logged successfully, otherwise the uuid[] of notificationids that were skipped because a log entry already existed for them.
Supersedes notifications.insert_notification_log(uuid), which is kept for backward compatibility and logs pending notifications too.';
