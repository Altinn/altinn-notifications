-- FUNCTION: notifications.claim_anytime_sms_v2()
CREATE OR REPLACE FUNCTION notifications.claim_anytime_sms_v2 ()
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
  WITH claimed_new_rows AS (
    SELECT sms._id, sms._orderid, ord.creatorname
    FROM notifications.smsnotifications sms
    JOIN notifications.orders ord ON ord._id = sms._orderid
    WHERE sms.result = 'New'::smsnotificationresulttype
      AND sms.expirytime >= now()
      AND ord.sendingtimepolicy = 1
    ORDER BY sms._id
    FOR UPDATE OF sms SKIP LOCKED
    LIMIT 1
  ),
  updated_rows AS (
    UPDATE notifications.smsnotifications sms
    SET resulttime = now(),
        result = 'Sending'::smsnotificationresulttype
    FROM claimed_new_rows claimed
    WHERE sms._id = claimed._id
    RETURNING
      sms._orderid,
      sms.alternateid,
      sms.mobilenumber,
      sms.customizedbody,
      claimed.creatorname
  )
  SELECT
    upd.alternateid,
    txt.sendernumber,
    upd.mobilenumber,
    COALESCE(NULLIF(upd.customizedbody, ''), txt.body) AS body,
    upd.creatorname
  FROM updated_rows upd
  JOIN notifications.smstexts txt ON txt._orderid = upd._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_anytime_sms_v2() IS
'Claims and returns an SMS notification (sendingtimepolicy = 1).
Includes the order creatorname to support per-service-owner SMS sender substitution.';