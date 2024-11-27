CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, sendernumber text, mobilenumber text, body text, recipientorgno text, recipientnin text) 
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN query 
    WITH updated AS (
        UPDATE notifications.smsnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.smsnotifications.alternateid, _orderid, notifications.smsnotifications.mobilenumber, notifications.smsnotifications.recipientorgno, notifications.smsnotifications.recipientnin)
    SELECT u.alternateid, st.sendernumber, u.mobilenumber, st.body, u.recipientorgno, u.recipientnin
    FROM updated u, notifications.smstexts st
    WHERE u._orderid = st._orderid;        
END;
$BODY$;