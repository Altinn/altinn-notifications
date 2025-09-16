-- FUNCTION: notifications.claim_sms_batch_for_sending(integer, integer)
CREATE OR REPLACE FUNCTION notifications.claim_sms_batch_for_sending (
  _sendingtimepolicy integer,
  _batchsize integer DEFAULT NULL
)
RETURNS TABLE (
  alternateid uuid,
  sendernumber text,
  mobilenumber text,
  body text
)
LANGUAGE plpgsql
COST 100
VOLATILE
PARALLEL UNSAFE
ROWS 10000
AS $$
DECLARE
  v_batchsize integer := GREATEST(1, COALESCE(_batchsize, 1000));
BEGIN
  RETURN QUERY
  WITH claimed_new_rows AS (
    SELECT sms._id, sms._orderid
    FROM notifications.smsnotifications sms
    JOIN notifications.orders ord ON ord._id = sms._orderid
    WHERE sms.result = 'New'::smsnotificationresulttype
      AND (
            (_sendingtimepolicy = 1 AND ord.sendingtimepolicy = 1)
         OR (_sendingtimepolicy = 2 AND (ord.sendingtimepolicy = 2 OR ord.sendingtimepolicy IS NULL))
      )
    ORDER BY sms._id
    FOR UPDATE OF sms SKIP LOCKED
    LIMIT v_batchsize
  ),
  updated_rows AS (
    UPDATE notifications.smsnotifications sms
    SET result = 'Sending'::smsnotificationresulttype,
        resulttime = now()
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

COMMENT ON FUNCTION notifications.claim_sms_batch_for_sending(INTEGER, INTEGER) IS
'Claims and returns batches of SMS notifications ready for sending.
_sendingtimepolicy: 1 (Anytime) or 2 (Daytime; NULL order policy treated as Daytime)
_batchsize: Requested batch size (no internal hard cap; fallback 1000 if NULL).';