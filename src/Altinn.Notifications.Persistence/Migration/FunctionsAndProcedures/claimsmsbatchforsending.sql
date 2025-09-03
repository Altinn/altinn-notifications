
-- FUNCTION: notifications.claim_sms_batch_for_sending(integer, integer DEFAULT 50)
CREATE OR REPLACE FUNCTION notifications.claim_sms_batch_for_sending (
  _sendingtimepolicy integer,
  _batchsize integer DEFAULT 50
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
ROWS 1000
AS $$
BEGIN
  RETURN QUERY
  WITH to_process AS (
    SELECT s._id, s._orderid
    FROM notifications.smsnotifications s
    JOIN notifications.orders o ON s._orderid = o._id
    WHERE s.result = 'New'
      AND (
           (_sendingtimepolicy = 1 AND o.sendingtimepolicy = 1)
        OR (_sendingtimepolicy = 2 AND (o.sendingtimepolicy = 2 OR o.sendingtimepolicy IS NULL))
      )
    FOR UPDATE OF s, o SKIP LOCKED
    LIMIT GREATEST(1, COALESCE(_batchsize, 50))
  ),
  updated AS (
    UPDATE notifications.smsnotifications s
    SET result = 'Sending',
        resulttime = now()
    FROM to_process tp
    WHERE s._id = tp._id
    RETURNING
      s.alternateid,
      s._orderid,
      s.mobilenumber,
      s.customizedbody
  )
  SELECT
    u.alternateid,
    st.sendernumber,
    u.mobilenumber,
    CASE
      WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' THEN u.customizedbody
      ELSE st.body
    END AS body
  FROM updated u
  JOIN notifications.smstexts st ON u._orderid = st._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_sms_batch_for_sending(INTEGER, INTEGER) IS 
'Atomically claims and returns batches of SMS notifications ready for sending.

Parameters:
  _sendingtimepolicy - Controls which notifications to process:
    1 (Daytime): Only process notifications with Daytime policy
    2 (Anytime): Process notifications with Anytime policy or NULL policy (treated as Daytime)
  
  _batchsize - Maximum number of notifications to claim in a single call (default: 50)
  
The function:
  1. Uses FOR UPDATE SKIP LOCKED to allow concurrent workers without contention
  2. Atomically transitions notifications from "New" to "Sending" status
  3. Returns notification details including alternateid, sender number, recipient number, and message body
  
Each row is guaranteed to be processed by only one caller, making this function
safe for concurrent use across multiple application instances.';