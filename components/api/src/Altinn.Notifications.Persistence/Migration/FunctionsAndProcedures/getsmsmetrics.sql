-- FUNCTION: notifications.get_sms_metrics(integer, integer, integer)
CREATE OR REPLACE FUNCTION notifications.get_sms_metrics(
    day_input integer,
	month_input integer,
	year_input integer)
    RETURNS TABLE(sms_id bigint, shipmentid uuid, senders_reference text, requestedsendtime timestamptz, creatorname text, resourceid text, result text, gatewayreference text, rate text, mobilenumber_prefix text, altinn_sms_count integer, altinn_sms_custom_body_length integer, altinn_sms_body_length integer) 
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
   SELECT s.sms_id, s.shipmentid, s.senders_reference, s.requestedsendtime, s.creatorname, s.resourceid, s.result, s.gatewayreference, s.rate, s.mobilenumber_prefix, s.altinn_sms_count, s.altinn_sms_custom_body_length, s.altinn_sms_body_length
     FROM notifications.sms_metrics_recent s
     WHERE s.resulttime >= start_date
       AND s.resulttime < start_date + INTERVAL '1 day';
   
END;
$BODY$;

ALTER FUNCTION notifications.get_sms_metrics(integer, integer, integer)
    OWNER TO platform_notifications_admin;
