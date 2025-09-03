-- This function is kept for backward compatibility and may be removed in future versions.
-- Use notifications.getsms_statusnew_updatestatus(integer) instead.
CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, sendernumber text, mobilenumber text, body text) 
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY 
    WITH updated AS (
        UPDATE notifications.smsnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.smsnotifications.alternateid, 
                  _orderid, 
                  notifications.smsnotifications.mobilenumber,
                  notifications.smsnotifications.customizedbody
    )
    SELECT u.alternateid, 
           st.sendernumber, 
           u.mobilenumber, 
           CASE WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' THEN u.customizedbody ELSE st.body END AS body
    FROM updated u
    JOIN notifications.smstexts st ON u._orderid = st._orderid;        
END;
$BODY$;

-- FUNCTION: notifications.getsms_statusnew_updatestatus(integer)
CREATE OR REPLACE FUNCTION NOTIFICATIONS.GETSMS_STATUSNEW_UPDATESTATUS (_SENDINGTIMEPOLICY INTEGER) 
RETURNS TABLE (
    ALTERNATEID UUID,
    SENDERNUMBER TEXT,
    MOBILENUMBER TEXT,
    BODY TEXT
) 
LANGUAGE 'plpgsql' 
COST 100 
VOLATILE 
PARALLEL UNSAFE 
ROWS 1000 
AS $BODY$
BEGIN
    RETURN QUERY 
    WITH updated AS (
        UPDATE notifications.smsnotifications s
        SET result = 'Sending', resulttime = now()
        FROM notifications.orders o
        WHERE s.result = 'New' 
          AND s._orderid = o._id
          AND (
              (_sendingtimepolicy = 1 AND o.sendingtimepolicy = 1)
           OR (_sendingtimepolicy = 2 AND (o.sendingtimepolicy = 2 OR o.sendingtimepolicy IS NULL))
          )
        RETURNING s.alternateid, 
                  s._orderid, 
                  s.mobilenumber,
                  s.customizedbody
    )
    SELECT u.alternateid, 
           st.sendernumber, 
           u.mobilenumber, 
           CASE 
               WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' 
               THEN u.customizedbody 
               ELSE st.body 
           END AS body
    FROM updated u
    JOIN notifications.smstexts st ON u._orderid = st._orderid;
END;
$BODY$;

COMMENT ON FUNCTION NOTIFICATIONS.GETSMS_STATUSNEW_UPDATESTATUS (INTEGER) IS 'Reads all entries in smsnotifications where result status is New.
 Result is then updated to Sending. Parameter _sendingtimepolicy is used to
 filter the returned entries based on the policy for scheduling set on the related
 order row. If this is null, it is treated as Daytime, which is the default setting';

-- FUNCTION: notifications.getsms_statusnew_updatestatus_v2(integer, integer DEFAULT 50)
CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus_v2 (
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