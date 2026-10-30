-- New version v2 introduced in https://github.com/Altinn/altinn-notifications/issues/1880
-- FUNCTION: notifications.get_email_metrics_v2(integer, integer, integer)
CREATE OR REPLACE FUNCTION notifications.get_email_metrics_v2(
    day_input integer,
	month_input integer,
	year_input integer)
    RETURNS TABLE(email_id bigint, email_shipmentid text, order_shipmentid text, senders_reference text, requestedsendtime timestamptz, resulttime timestamptz, creatorname text, resourceid text, result text, operationid text) 
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
 SELECT e.email_id, e.email_shipmentid, e.order_shipmentid, e.senders_reference, e.requestedsendtime, e.resulttime, e.creatorname, e.resourceid, e.result, e.operationid
    FROM notifications.email_metrics_recent_v2 e
    WHERE e.resulttime >= start_date
      AND e.resulttime < start_date + INTERVAL '1 day';

END;
$BODY$;

ALTER FUNCTION notifications.get_email_metrics_v2(integer, integer, integer)
    OWNER TO platform_notifications_admin;
