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
  select 
    -- references and correlation
    sms._id as sms_id --unique per number/"functional SMS"
,   sms.alternateid as shipmentid --unique per notification/reminder (but the same for the same notification/reminder to multiple recipients, e.g. to an organization where people with access to the resource have custom contact information)
,   orders.notificationorder ->> 'SendersReference' as senders_reference --senders reference (not necessarily unique)
,   orders.requestedsendtime --requested sending time (to determine when it is correct to invoice, if applicable. May differ slightly from actual sending time, so check in combination with status/gateway ref)
,   orders.creatorname --orderer's maskinporten ID (the real service owner can be hidden by aggregation, e.g. correspondence)    
,   orders.notificationorder ->> 'ResourceId' as resourceid --resourceid, can in combination with creatorname provide real service owner or granulation corresponding to service code etc.

     -- operator status
,   sms.result::text as result -- status of the delivery (error, but with a GW reference may mean that the message was attempted sent/tariffed, but for various reasons did not reach the user)
,   sms.gatewayreference -- reference at Link Mobility (a reference likely means that Link Mobility bills for this — but not necessarily)

     -- tariffing (price group + message splitting by the operator)
,   CASE 
        WHEN sms.mobilenumber IS NULL OR sms.mobilenumber = '' THEN 'n/a'
        WHEN sms.mobilenumber ~ '^(\+|00) *47' THEN 'innland' 
        ELSE 'utland' 
    END as rate  -- all numbers that are not Norwegian are defined in the international rate group.  
,   left(sms.mobilenumber, 4) as mobilenumber_prefix --temporary field to verify the rate field
,   sms.smscount as altinn_sms_count -- internal counting logic to split messages over 160 characters
,   length(sms.customizedbody) as altinn_sms_custom_body_length --number of characters in the text (for messages with keywords)
,   length(sms_text.body) as altinn_sms_body_length --number of characters in the text received from the  
     
--,  sms.*, sms_text.*, orders.*, order_chain.*
from notifications.smsnotifications as sms
         inner join notifications.orders orders on orders._id = sms._orderid
         left join notifications.smstexts sms_text on orders._id = sms_text._orderid
         WHERE sms.resulttime >= start_date
			AND sms.resulttime < start_date + INTERVAL '1 day'
            AND sms.result NOT IN ('New',
                           'Sending',
                           'Accepted');
   
END;
$BODY$;

ALTER FUNCTION notifications.get_sms_metrics(integer, integer, integer)
    OWNER TO platform_notifications_admin;
