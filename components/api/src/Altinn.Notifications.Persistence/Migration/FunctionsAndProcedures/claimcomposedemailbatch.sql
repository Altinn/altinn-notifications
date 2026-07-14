CREATE OR REPLACE FUNCTION notifications.claim_composed_email_batch(
    _batchsize integer DEFAULT NULL::integer)
    RETURNS TABLE(
        alternateid  uuid,
        subject      text,
        body         text,
        fromaddress  text,
        toaddress    text,
        contenttype  text,
        attachments  jsonb
    )
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    v_batchsize integer := GREATEST(1, COALESCE(_batchsize, 500));
    latest_email_timeout timestamp;
    v_limitlog_id integer;
BEGIN
    SELECT id, emaillimittimeout
    INTO v_limitlog_id, latest_email_timeout
    FROM notifications.resourcelimitlog
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)
    FOR UPDATE;

    -- Check for active email timeout.
    IF latest_email_timeout IS NOT NULL AND latest_email_timeout > now() THEN
        RETURN;
    ELSE
        UPDATE notifications.resourcelimitlog
        SET emaillimittimeout = NULL
        WHERE id = v_limitlog_id;
    END IF;

    RETURN QUERY
    WITH claimed_new_rows AS (
        SELECT
            email._id,
            email.alternateid,
            email.customizedsubject,
            email.customizedbody,
            email.toaddress,
            email._orderid
        FROM notifications.emailnotifications email
        JOIN notifications.orders o ON o._id = email._orderid
        WHERE email.result = 'New'::emailnotificationresulttype
            AND email.expirytime >= now()
            AND o.type = 'Composed'::notificationordertype
        ORDER BY email._id
        FOR UPDATE OF email SKIP LOCKED
        LIMIT v_batchsize
    ),
    updated_rows AS (
        UPDATE notifications.emailnotifications email
        SET resulttime = now(),
            result = 'Sending'::emailnotificationresulttype
        FROM claimed_new_rows claimed
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
        COALESCE(o.notificationorder -> 'EmailAttachments', '[]'::jsonb) AS attachments
    FROM updated_rows updated
    JOIN notifications.emailtexts txt ON txt._orderid = updated._orderid
    JOIN notifications.orders o       ON o._id = updated._orderid;
END;
$BODY$;

ALTER FUNCTION notifications.claim_composed_email_batch(integer)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_composed_email_batch(integer)
    IS 'Claims and returns batches of email notifications for Composed orders (OrderType = 3).
Returns an empty JSON array when no attachments are present.
_batchsize: requested batch size (defaults to 500 if NULL or <1).';
