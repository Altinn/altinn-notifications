-- FUNCTION: notifications.claim_daytime_sms()
CREATE OR REPLACE FUNCTION notifications.claim_daytime_sms()
RETURNS TABLE (
  alternateid uuid,
  sendernumber text,
  mobilenumber text,
  body text
)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  WITH claimed_new_rows AS (
    SELECT sms._id, sms._orderid
    FROM notifications.smsnotifications sms
    JOIN notifications.orders ord ON ord._id = sms._orderid
    WHERE sms.result = 'New'::smsnotificationresulttype
      AND sms.expirytime >= now()
      AND (ord.sendingtimepolicy = 2 OR ord.sendingtimepolicy IS NULL)
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
      sms.customizedbody
  )
  SELECT
    upd.alternateid,
    txt.sendernumber,
    upd.mobilenumber,
    COALESCE(NULLIF(upd.customizedbody, ''), txt.body) AS body
  FROM updated_rows upd
  JOIN notifications.smstexts txt ON txt._orderid = upd._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_daytime_sms() IS
'Claims and returns batches of SMS notifications (sendingtimepolicy = 2 or NULL).';