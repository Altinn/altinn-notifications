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

-- Function: notifications.set_sent_target
CREATE OR REPLACE FUNCTION notifications.update_senttarget(_id bigint)
     RETURNS TABLE (
        id bigint,
        notificationid bigint,
        channeltype character varying,
        "address" character varying,
        "sent" timestamptz)
    LANGUAGE 'plpgsql'

AS $BODY$
DECLARE currentTime timestamptz;
BEGIN
 SET TIME ZONE UTC;
  currentTime := NOW();

RETURN QUERY
    UPDATE notifications.targets
	SET "sent" = currentTime
    WHERE id = _id;
END;
$BODY$;
