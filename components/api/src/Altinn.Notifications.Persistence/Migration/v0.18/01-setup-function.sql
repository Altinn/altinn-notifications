CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, sendernumber text, mobilenumber text, body text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000
AS $BODY$
BEGIN
		
    RETURN query 
    WITH updated AS (
        UPDATE notifications.smsnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.smsnotifications.alternateid, _orderid, notifications.smsnotifications.mobilenumber)
    SELECT u.alternateid, st.sendernumber, u.mobilenumber, st.body
    FROM updated u, notifications.smstexts st
    WHERE u._orderid = st._orderid;    
END;
$BODY$;

ALTER FUNCTION notifications.getsms_statusnew_updatestatus()
    OWNER TO platform_notifications_admin;
