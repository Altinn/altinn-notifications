-- Function: notifications.get_unsenttargets
CREATE OR REPLACE FUNCTION notifications.get_unsenttargets()
    RETURNS TABLE (
        id bigint,
        notificationid bigint,
        channeltype character varying,
        "address" character varying,
        "sent" timestamptz)
    LANGUAGE 'plpgsql'

AS $BODY$
BEGIN
return query
	SELECT t.id, t.notificationid, t.channeltype, t."address", t."sent"
    	FROM notifications.targets t
	WHERE t."sent" IS NULL;
	

END;
$BODY$;

-- Function: notifications.update_senttarget
CREATE OR REPLACE PROCEDURE notifications.update_senttarget(
	_id bigint)
    LANGUAGE 'plpgsql'
  

AS $BODY$
DECLARE currentTime timestamptz;
BEGIN
 SET TIME ZONE UTC;
  currentTime := NOW();
 
    UPDATE notifications.targets as t
	SET "sent" = currentTime
    WHERE t.id = _id;
$BODY$;
