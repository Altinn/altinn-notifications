CREATE OR REPLACE FUNCTION notifications.claim_retry_anytime_sms(_retry_delay interval)
RETURNS TABLE (
  alternateid uuid,
  sendernumber text,
  mobilenumber text,
  body text,
  creatorname text
)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  WITH claimed_retry_rows AS (
    SELECT sms._id, sms._orderid
    FROM notifications.smsnotifications sms
    WHERE sms.result = 'Retrying'::smsnotificationresulttype
      AND sms.resulttime <= now() - _retry_delay
      AND sms.expirytime >= now()
      AND sms.sendingtimepolicy = 1
    ORDER BY sms.resulttime, sms._id
    FOR UPDATE OF sms SKIP LOCKED
    LIMIT 1
  ),
  updated_rows AS (
    UPDATE notifications.smsnotifications sms
    SET resulttime = now(),
        result = 'Sending'::smsnotificationresulttype
    FROM claimed_retry_rows claimed
    WHERE sms._id = claimed._id
    RETURNING
      sms._orderid,
      sms.alternateid,
      sms.mobilenumber,
      sms.customizedbody
  )
  SELECT
    upd.alternateid,
    txt.sendernumber,
    upd.mobilenumber,
    COALESCE(NULLIF(upd.customizedbody, ''), txt.body) AS body,
    ord.creatorname
  FROM updated_rows upd
  JOIN notifications.smstexts txt ON txt._orderid = upd._orderid
  JOIN notifications.orders ord ON ord._id = upd._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_retry_anytime_sms(interval) IS
'Claims and returns an SMS notification in status Retrying with sending time policy Anytime (sendingtimepolicy = 1).
Only notifications that entered Retrying at least _retry_delay ago and have not expired are claimed.
Includes the order creatorname to support per-service-owner SMS sender substitution.';
