CREATE OR REPLACE FUNCTION notifications.getemails_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000
AS $BODY$
DECLARE
    latest_email_timeout TIMESTAMP WITH TIME ZONE;
BEGIN
    SELECT emaillimittimeout INTO latest_email_timeout FROM notifications.resourcelimitlog WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);
    IF latest_email_timeout IS NULL THEN
        RETURN query 
            WITH updated AS (
                UPDATE notifications.emailnotifications
                    SET result = 'Sending', resulttime = now()
                    WHERE result = 'New' 
                    RETURNING notifications.emailnotifications.alternateid, _orderid, notifications.emailnotifications.toaddress)
            SELECT u.alternateid, et.subject, et.body, et.fromaddress, u.toaddress, et.contenttype 
            FROM updated u, notifications.emailtexts et
            WHERE u._orderid = et._orderid;    
    ELSE 
         RETURN QUERY SELECT * FROM notifications.emailtexts WHERE FALSE; 
    END IF;
END;
$BODY$;

ALTER FUNCTION notifications.getemails_statusnew_updatestatus()
    OWNER TO platform_notifications_admin;
