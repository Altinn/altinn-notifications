CREATE OR REPLACE FUNCTION notifications.claim_email_batch(
    _batchsize integer DEFAULT NULL::integer)
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
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
    FOR UPDATE SKIP LOCKED;
    
    -- Check if lock is taken
    IF v_limitlog_id IS NULL THEN
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

    -- Check if there's an active email timeout
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

    -- Clear expired timeout
    UPDATE notifications.resourcelimitlog
    SET emaillimittimeout = NULL
    WHERE id = v_limitlog_id
        AND emaillimittimeout IS NOT NULL 
        AND emaillimittimeout <= now();

    RETURN QUERY
    WITH claimed_new_rows AS (
        SELECT
            email._id,
            email._orderid,
            email.alternateid,
            email.toaddress,
            email.customizedsubject,
            email.customizedbody
        FROM notifications.emailnotifications email
        WHERE email.result = 'New'::emailnotificationresulttype
            AND email.expirytime >= now()
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
            claimed._orderid,
            claimed.toaddress,
            claimed.customizedsubject,
            claimed.customizedbody
    )
    -- Join with large text data AFTER releasing locks
    -- Use customized subject/body if available, otherwise fall back to template
    SELECT
        updated.alternateid,
        CASE WHEN updated.customizedsubject IS NOT NULL AND updated.customizedsubject <> ''
             THEN updated.customizedsubject
             ELSE txt.subject END AS subject,
        CASE WHEN updated.customizedbody IS NOT NULL AND updated.customizedbody <> ''
             THEN updated.customizedbody
             ELSE txt.body END AS body,
        txt.fromaddress,
        updated.toaddress,
        txt.contenttype
    FROM updated_rows updated
    JOIN notifications.emailtexts txt ON txt._orderid = updated._orderid;
END;
$BODY$;

ALTER FUNCTION notifications.claim_email_batch(integer)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_email_batch(integer)
    IS 'Claims and returns batches of email notifications.
_batchsize: requested batch size (defaults to 500 if NULL or <1).';
