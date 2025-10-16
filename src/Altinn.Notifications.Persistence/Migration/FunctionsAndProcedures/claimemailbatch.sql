CREATE OR REPLACE FUNCTION notifications.claim_email_batch(
    _batchsize integer DEFAULT NULL::integer)
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000
AS $BODY$
DECLARE
    v_batchsize integer := GREATEST(1, COALESCE(_batchsize, 500));
BEGIN
    RETURN QUERY
    WITH claimed_new_rows AS (
        SELECT 
            email._id, 
            email._orderid,
            email.alternateid,
            email.toaddress,
            txt.subject,
            txt.body,
            txt.fromaddress,
            txt.contenttype
        FROM notifications.emailnotifications email
        JOIN notifications.emailtexts txt ON txt._orderid = email._orderid
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
            claimed.toaddress,
            claimed.subject,
            claimed.body,
            claimed.fromaddress,
            claimed.contenttype
    )
    SELECT * FROM updated_rows;
END;
$BODY$;

ALTER FUNCTION notifications.claim_email_batch(integer)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_email_batch(integer)
    IS 'Claims and returns batches of email notifications.
_batchsize: requested batch size (defaults to 500 if NULL or <1).';
