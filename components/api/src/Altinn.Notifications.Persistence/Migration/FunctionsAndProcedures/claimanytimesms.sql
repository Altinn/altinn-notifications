-- FUNCTION: notifications.claim_anytime_sms_v4()
CREATE OR REPLACE FUNCTION notifications.claim_anytime_sms_v4 ()
RETURNS TABLE (
  alternateid uuid,
  sendernumber text,
  mobilenumber text,
  body text,
  creatorname text,
  conditionendpoint text,
  isconditionretry boolean
)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  WITH claimed_new_rows AS (
    SELECT sms._id, sms._orderid
    FROM notifications.smsnotifications sms
    WHERE sms.result = 'New'::smsnotificationresulttype
      AND sms.expirytime >= now()
      AND sms.sendingtimepolicy = 1
      AND (sms.nextattempt IS NULL OR sms.nextattempt <= now())
    ORDER BY sms.expirytime
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
      sms.nextattempt
  )
  SELECT
    upd.alternateid,
    txt.sendernumber,
    upd.mobilenumber,
    COALESCE(NULLIF(upd.customizedbody, ''), txt.body) AS body,
    ord.creatorname,
    ord.notificationorder->>'ConditionEndpoint' AS conditionendpoint,
    upd.nextattempt IS NOT NULL AS isconditionretry
  FROM updated_rows upd
  JOIN notifications.smstexts txt ON txt._orderid = upd._orderid
  JOIN notifications.orders ord ON ord._id = upd._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_anytime_sms_v4() IS
'Claims and returns an SMS notification (sendingtimepolicy = 1).
Includes the order creatorname to support per-service-owner SMS sender substitution,
and the order condition endpoint so the send condition can be evaluated at send time.';