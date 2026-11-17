CREATE OR REPLACE FUNCTION notifications.claim_retry_composed_email_v1(_retry_delay interval)
    RETURNS TABLE(
        alternateid  uuid,
        subject      text,
        body         text,
        fromaddress  text,
        toaddress    text,
        contenttype  text,
        attachments  jsonb,
        conditionendpoint text
    )
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    latest_email_timeout timestamp;
BEGIN
    SELECT emaillimittimeout
    INTO latest_email_timeout
    FROM notifications.resourcelimitlog
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);

    -- Check for active email timeout.
    IF latest_email_timeout IS NOT NULL AND latest_email_timeout > now() THEN
        RETURN;
    END IF;

    RETURN QUERY
    WITH claimed_retry_rows AS (
        SELECT
            email._id,
            email.alternateid,
            email.customizedsubject,
            email.customizedbody,
            email.toaddress,
            email._orderid
        FROM notifications.emailnotifications email
        WHERE email.result = 'Retrying'::emailnotificationresulttype
            AND email.resulttime <= now() - _retry_delay
            AND email.expirytime >= now()
            AND email.iscomposed = true
        ORDER BY email.resulttime, email._id
        FOR UPDATE OF email SKIP LOCKED
        LIMIT 1
    ),
    updated_rows AS (
        UPDATE notifications.emailnotifications email
        SET resulttime = now(),
            result = 'Sending'::emailnotificationresulttype
        FROM claimed_retry_rows claimed
        WHERE email._id = claimed._id
        RETURNING
            claimed.alternateid,
            claimed.customizedsubject,
            claimed.customizedbody,
            claimed.toaddress,
            claimed._orderid
    )
    SELECT
        updated.alternateid,
        COALESCE(NULLIF(updated.customizedsubject, ''), txt.subject) AS subject,
        COALESCE(NULLIF(updated.customizedbody, ''), txt.body)       AS body,
        txt.fromaddress,
        updated.toaddress,
        txt.contenttype,
        COALESCE(o.notificationorder -> 'EmailAttachments', '[]'::jsonb) AS attachments,
        o.notificationorder->>'ConditionEndpoint' AS conditionendpoint
    FROM updated_rows updated
    JOIN notifications.emailtexts txt ON txt._orderid = updated._orderid
    JOIN notifications.orders o       ON o._id = updated._orderid;
END;
$BODY$;

ALTER FUNCTION notifications.claim_retry_composed_email_v1(interval)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_retry_composed_email_v1(interval)
    IS 'Claims and returns a single composed email notification in status Retrying (after an inconclusive send condition check) that has waited at least _retry_delay.';
