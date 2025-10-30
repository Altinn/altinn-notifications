CREATE OR REPLACE FUNCTION notifications.getemails_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text) 
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    latest_email_timeout TIMESTAMP WITH TIME ZONE;
BEGIN
    SELECT emaillimittimeout 
    INTO latest_email_timeout 
    FROM notifications.resourcelimitlog 
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);

    -- Check if the latest email timeout is set and valid
    IF latest_email_timeout IS NOT NULL THEN
        IF latest_email_timeout >= NOW() THEN
            RETURN QUERY 
            SELECT NULL::uuid AS alternateid, 
                   NULL::text AS subject, 
                   NULL::text AS body, 
                   NULL::text AS fromaddress, 
                   NULL::text AS toaddress, 
                   NULL::text AS contenttype 
            WHERE FALSE;
            RETURN;
        ELSE 
            UPDATE notifications.resourcelimitlog 
            SET emaillimittimeout = NULL 
            WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);
        END IF;
    END IF;
    
    RETURN QUERY 
    WITH updated AS (
        UPDATE notifications.emailnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.emailnotifications.alternateid, 
                  _orderid, 
                  notifications.emailnotifications.toaddress, 
                  notifications.emailnotifications.customizedsubject, 
                  notifications.emailnotifications.customizedbody
    )
    SELECT u.alternateid, 
           CASE WHEN u.customizedsubject IS NOT NULL AND u.customizedsubject <> '' THEN u.customizedsubject ELSE et.subject END AS subject, 
           CASE WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' THEN u.customizedbody ELSE et.body END AS body, 
           et.fromaddress, 
           u.toaddress, 
           et.contenttype 
    FROM updated u
    JOIN notifications.emailtexts et ON u._orderid = et._orderid;    
END;
$BODY$;
