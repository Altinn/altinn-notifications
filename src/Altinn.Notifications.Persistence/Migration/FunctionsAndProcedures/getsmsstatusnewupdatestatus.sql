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
