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
  select 
    -- references and correlation
    email._id as email_id --unique recipient/"functional email"
,   email.alternateid as shipmentid --unique per notification/reminder (but the same for the same notification/reminder to multiple recipients, e.g. to an organization where people with access to the resource have custom contact information)
,   orders.notificationorder ->> 'SendersReference' as senders_reference --senders reference (not necessarily unique)
,   orders.requestedsendtime --requested sending time (to determine when it is correct to invoice, if applicable. May differ slightly from actual sending time, so check in combination with status/gateway ref)
,   orders.creatorname --orderer's maskinporten ID (the real service owner can be hidden by aggregation, e.g. correspondence)    
,   orders.notificationorder ->> 'ResourceId' as resourceid --resourceid, can in combination with creatorname provide real service owner or granulation corresponding to service code etc.

     -- operator status
,   email.result::text as result -- status of the delivery (error, but with an operationid may mean that the message was attempted sent/tariffed, but for various reasons did not reach the user)
,   email.operationid -- reference at ACS (a reference likely means that ACS bills for this — but not necessarily)

from notifications.emailnotifications as email
         inner join notifications.orders orders on orders._id = email._orderid
         WHERE email.resulttime >= start_date
			AND email.resulttime < start_date + INTERVAL '1 day'
            AND email.result NOT IN ('New',
                           'Sending',
                           'Succeeded');
END;
$BODY$;

ALTER FUNCTION notifications.get_email_metrics(integer, integer, integer)
    OWNER TO platform_notifications_admin;
