-- FUNCTION: notifications.claim_retry_sms_v1(boolean, interval)
CREATE OR REPLACE FUNCTION notifications.claim_retry_sms_v1 (_include_daytime boolean, _retry_delay interval)
RETURNS TABLE (
  alternateid uuid,
  sendernumber text,
  mobilenumber text,
  body text,
  creatorname text,
  conditionendpoint text
)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  WITH claimed_retry_rows AS (
    SELECT sms._id
    FROM notifications.smsnotifications sms
    WHERE sms.result = 'Retrying'::smsnotificationresulttype
      AND sms.resulttime <= now() - _retry_delay
      AND (sms.sendingtimepolicy = 1 OR (_include_daytime AND sms.sendingtimepolicy = 2))
    ORDER BY sms.resulttime, sms._id
    FOR UPDATE OF sms SKIP LOCKED
    LIMIT 1
  ),
  updated_rows AS (
    UPDATE notifications.smsnotifications sms
    SET
        resulttime = now(),
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
    ord.creatorname,
    ord.notificationorder->>'ConditionEndpoint' AS conditionendpoint
  FROM updated_rows upd
  JOIN notifications.smstexts txt ON txt._orderid = upd._orderid
  JOIN notifications.orders ord ON ord._id = upd._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_retry_sms_v1(boolean, interval) IS
'Claims and returns a single SMS notification in status Retrying (after an inconclusive send condition check) that has waited at least _retry_delay.
Daytime notifications (sendingtimepolicy = 2) are only claimed when _include_daytime is true.
Expirytime is deliberately not checked, so a retry that becomes due after expiry is not lost.';
