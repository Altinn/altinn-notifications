
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
