-- FUNCTION: notifications.claim_sms_batch_for_sending(integer, integer DEFAULT 50)
CREATE OR REPLACE FUNCTION notifications.claim_sms_batch_for_sending (
  _sendingtimepolicy integer,
  _batchsize integer DEFAULT 1000
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
DECLARE
  v_batchsize integer := GREATEST(1, LEAST(COALESCE(_batchsize, 1000), 1000));
BEGIN
  RETURN QUERY
  WITH to_process AS (
    SELECT s._id, s._orderid
    FROM notifications.smsnotifications s
    WHERE s.result = 'New'
      AND EXISTS (
            SELECT 1
            FROM notifications.orders o
            WHERE o._id = s._orderid
              AND (
                   (_sendingtimepolicy = 1 AND o.sendingtimepolicy = 1)
                OR (_sendingtimepolicy = 2 AND (o.sendingtimepolicy = 2 OR o.sendingtimepolicy IS NULL))
              )
      )
    ORDER BY s._id
    FOR UPDATE OF s SKIP LOCKED
    LIMIT v_batchsize
  ),
  updated AS (
    UPDATE notifications.smsnotifications s
    SET result = 'Sending',
        resulttime = now()
    FROM to_process tp
    WHERE s._id = tp._id
    RETURNING
      s._orderid,
      s.alternateid,
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
  JOIN notifications.smstexts st ON st._orderid = u._orderid;
END;
$$;

COMMENT ON FUNCTION notifications.claim_sms_batch_for_sending(INTEGER, INTEGER) IS 
'Atomically claims and returns batches of SMS notifications ready for sending.

Parameters:
  _sendingtimepolicy - 1 (Anytime) or 2 (Daytime; NULL is treated as Daytime)
  _batchsize - Max notifications claimed (default: 1000; clamped to [1,1000])';