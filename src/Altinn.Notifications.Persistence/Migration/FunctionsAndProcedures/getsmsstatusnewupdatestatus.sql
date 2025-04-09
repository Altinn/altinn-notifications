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
CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus(
	_sendingtimepolicy integer)
    RETURNS TABLE(alternateid uuid, sendernumber text, mobilenumber text, body text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

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
    JOIN notifications.smstexts st ON u._orderid = st._orderid
	JOIN notifications.orders o ON st._orderid = o._id 
	-- sendingTimePolicy = 2 is equal to daytime, the default choice when null
	WHERE o.sendingtimepolicy = _sendingtimepolicy OR (o.sendingtimepolicy IS NULL AND _sendingtimepolicy = 2);        
END;
$BODY$;

COMMENT ON function notifications.getsms_statusnew_updatestatus(
	_sendingtimepolicy integer) is
'Reads all entries in smsnotifications where result status is New.
 Result is then updated to Sending. Parameter _sendingtimepolicy is used to
 filter the returned entries based on the policy for scheduling set on the related
 order row. If this is null, it is treated as Daytime, which is the default setting';
