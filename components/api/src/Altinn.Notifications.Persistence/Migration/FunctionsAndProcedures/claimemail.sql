CREATE OR REPLACE FUNCTION notifications.claim_email()
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text)
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    latest_email_timeout timestamp;
    v_limitlog_id integer;
BEGIN
    SELECT id, emaillimittimeout
    INTO v_limitlog_id, latest_email_timeout
    FROM notifications.resourcelimitlog
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);

    -- Check for active email timeout.
    IF latest_email_timeout IS NOT NULL AND latest_email_timeout > now() THEN
        RETURN QUERY
        SELECT NULL::uuid AS alternateid,
               NULL::text AS subject,
               NULL::text AS body,
               NULL::text AS fromaddress,
               NULL::text AS toaddress,
               NULL::text AS contenttype
        WHERE FALSE;
        RETURN;
    END IF;

    UPDATE notifications.resourcelimitlog
    SET emaillimittimeout = NULL
    WHERE id = v_limitlog_id
        AND emaillimittimeout IS NOT NULL
        AND emaillimittimeout <= now();

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
            AND o.type <> 'Composed'::notificationordertype
        ORDER BY email._id
        FOR UPDATE OF email SKIP LOCKED
        LIMIT 1
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
    -- Join with large text data AFTER releasing locks
    SELECT
        updated.alternateid,
        COALESCE(NULLIF(updated.customizedsubject, ''), txt.subject) AS subject,
        COALESCE(NULLIF(updated.customizedbody, ''), txt.body) AS body,
        txt.fromaddress,
        updated.toaddress,
        txt.contenttype
    FROM updated_rows updated
    JOIN notifications.emailtexts txt ON txt._orderid = updated._orderid;
END;
$BODY$;

ALTER FUNCTION notifications.claim_email()
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_email()
    IS 'Claims and returns a single email notification, excluding Composed orders (OrderType = 3).
Composed orders are processed through a dedicated pipeline to prevent head-of-line blocking.';
