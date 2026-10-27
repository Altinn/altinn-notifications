-- FUNCTION: notifications.get_email_metrics(integer, integer, integer)
CREATE OR REPLACE FUNCTION notifications.get_email_metrics(
    day_input integer,
	month_input integer,
	year_input integer)
    RETURNS TABLE(email_id bigint, shipmentid uuid, senders_reference text, requestedsendtime timestamptz, creatorname text, resourceid text, result text, operationid text) 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE PARALLEL SAFE
    ROWS 100000

AS $BODY$
DECLARE
  start_date TIMESTAMPTZ;
BEGIN
  start_date = MAKE_TIMESTAMPTZ(year_input, month_input, day_input, 0, 0, 0, 'UTC');

  RETURN QUERY
 SELECT e.email_id, e.shipmentid, e.senders_reference, e.requestedsendtime, e.creatorname, e.resourceid, e.result, e.operationid
    FROM notifications.email_metrics_recent e
    WHERE e.resulttime >= start_date
      AND e.resulttime < start_date + INTERVAL '1 day';

END;
$BODY$;

ALTER FUNCTION notifications.get_email_metrics(integer, integer, integer)
    OWNER TO platform_notifications_admin;
