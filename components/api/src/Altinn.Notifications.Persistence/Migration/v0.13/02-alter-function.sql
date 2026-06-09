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
		IF latest_email_timeout IS NOT NULL THEN
			IF latest_email_timeout >= NOW() THEN
				RETURN QUERY SELECT NULL::uuid AS alternateid, NULL::text AS subject, NULL::text AS body, NULL::text AS fromaddress, NULL::text AS toaddress, NULL::text AS contenttype WHERE FALSE;
			ELSE 
				UPDATE notifications.resourcelimitlog SET emaillimittimeout = NULL WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);
			END IF;
		END IF;
		
        RETURN query 
            WITH updated AS (
                UPDATE notifications.emailnotifications
                    SET result = 'Sending', resulttime = now()
                    WHERE result = 'New' 
                    RETURNING notifications.emailnotifications.alternateid, _orderid, notifications.emailnotifications.toaddress)
            SELECT u.alternateid, et.subject, et.body, et.fromaddress, u.toaddress, et.contenttype 
            FROM updated u, notifications.emailtexts et
            WHERE u._orderid = et._orderid;    
END;
$BODY$;

ALTER FUNCTION notifications.getemails_statusnew_updatestatus()
    OWNER TO platform_notifications_admin;
