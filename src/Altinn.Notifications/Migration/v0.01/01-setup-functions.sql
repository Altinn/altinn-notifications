
-- Function: notifications.insert_notification

CREATE OR REPLACE FUNCTION notifications.insert_notification(
    sendtime timestamptz,
    instanceid character varying,
    partyreference character varying,
    sender character varying)
  RETURNS SETOF notifications.notifications AS
$BODY$

BEGIN
  SET TIME ZONE UTC;

  RETURN QUERY
  INSERT INTO notifications.notifications(sendtime, instanceid, partyreference, sender)
  VALUES ($1, $2, $3, $4) RETURNING *;

END
$BODY$ LANGUAGE 'plpgsql';

-- Function: notifications.get_notification

CREATE OR REPLACE FUNCTION notifications.get_notification(_id bigint)
    RETURNS TABLE (
        id bigint, 
        sendtime timestamptz, 
        instanceid character varying, 
        partyreference character varying, 
        sender character varying)
    LANGUAGE 'plpgsql'
    
AS $BODY$
BEGIN
return query 
	SELECT n.id, n.sendtime, n.instanceid, n.partyreference, n.sender
	FROM notifications.notifications n
    LEFT JOIN notifications.targets t ON n.id = t.notificationid
    WHERE n.id = _id;

END;
$BODY$;

-- Function: notifications.insert_target

CREATE OR REPLACE FUNCTION notifications.insert_target(
    notificationid bigint,
    channeltype character varying,
    "address" character varying,
    "sent" timestamptz)
  RETURNS SETOF notifications.targets AS
$BODY$

BEGIN
  SET TIME ZONE UTC;

  RETURN QUERY
  INSERT INTO notifications.targets(notificationid, channeltype, "address", "sent")
  VALUES ($1, $2, $3, $4) RETURNING *;

END
$BODY$ LANGUAGE 'plpgsql';

-- Function: notifications.insert_message

CREATE OR REPLACE FUNCTION notifications.insert_message(
    notificationid bigint,
    emailsubject character varying,
    emailbody character varying,
    smstext character varying,
    "language" character varying)
  RETURNS SETOF notifications.messages AS
$BODY$

BEGIN
  SET TIME ZONE UTC;

  RETURN QUERY
  INSERT INTO notifications.messages(notificationid, emailsubject, emailbody, smstext, "language")
  VALUES ($1, $2, $3, $4, $5) RETURNING *;

END
$BODY$ LANGUAGE 'plpgsql';
