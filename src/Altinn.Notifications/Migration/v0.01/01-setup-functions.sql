
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
        sender character varying,
        targetid bigint,
        channeltype character varying,
        "address" character varying,
        "sent" timestamptz,
        messageid bigint,
        emailsubject character varying,
        emailbody character varying,
        smstext character varying,
        "language" character varying)
    LANGUAGE 'plpgsql'
    
AS $BODY$
BEGIN
return query 
	SELECT n.id, n.sendtime, n.instanceid, n.partyreference, n.sender, t.id, t.channeltype, t."address", t."sent", m.id, m.emailsubject, m.emailbody, m.smstext, m."language"
	FROM notifications.notifications n
    LEFT JOIN notifications.targets t ON n.id = t.notificationid
    LEFT JOIN notifications.messages m ON n.id = m.notificationid
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

-- Function: notifications.get_target

CREATE OR REPLACE FUNCTION notifications.get_target(_id bigint)
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
    WHERE t.id = _id;

END;
$BODY$;

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
	FROM notifications.targets t;

END;
$BODY$;