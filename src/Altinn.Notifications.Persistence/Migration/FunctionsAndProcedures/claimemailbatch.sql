CREATE OR REPLACE FUNCTION notifications.claim_email_batch(
  _batchsize integer DEFAULT NULL::integer)
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text) 
    LANGUAGE plpgsql
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$
DECLARE
  v_batchsize integer := GREATEST(1, COALESCE(_batchsize, 500));
BEGIN
  RETURN QUERY
  WITH claimed_new_rows AS (
    SELECT email._id, email._orderid
    FROM notifications.emailnotifications email
    JOIN notifications.orders ord ON ord._id = email._orderid
    WHERE email.result = 'New'::emailnotificationresulttype
      AND email.expirytime >= now()
      AND ord.sendingtimepolicy = 1
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
      email.alternateid,
      email.toaddress
  )
  SELECT
    upd.alternateid,
    txt.subject,
    txt.body,
    txt.fromaddress,
    upd.toaddress
  FROM updated_rows upd
  JOIN notifications.emailtexts txt ON txt._orderid = upd._orderid;
END;
$BODY$;

ALTER FUNCTION notifications.claim_email_batch(integer)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.claim_email_batch(integer)
    IS 'Claims and returns batches of email notifications.
_batchsize: requested batch size (defaults to 500 if NULL or <1).';
