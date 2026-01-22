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
    -- referanser og korrelering
    sms._id as sms_id --unik pr nummer/"funksjonell sms"
,   sms.alternateid as shipmentid --unik pr varsel/påminnelse (men lik ved samme varsel/påminnelse til flere mottakere, f.eks til org der personer med tilgang til ressursen har egendefinert kontaktinformasjon)
,   orders.notificationorder ->> 'SendersReference' as senders_reference --avsenders referanse (ikke nødvendigvis unik)    
,   orders.requestedsendtime --ønsket sendetidspunkt (for å avgjøre når det er riktig å evt. fakturere. Kan avvike noe fra faktisk sendetidspunkt, så se i kombinasjon med status/gateway-ref)
,   orders.creatorname -- bestillers maksinporten-id (den reelle tjenesteeieren kan skjules ved aggregering, f.eks correspondence)     
,   orders.notificationorder ->> 'ResourceId' as resourceid --ressurs-id, kan i kombinasjon med creator gi reell tjenesteeier eller granulering tilsvarende service-code mv.

     -- operatør-status
,   sms.result::text as result -- status på utsendingen (feil, men med GW-ref kan bety at meldingen er forsøkt sendt/taksert, men har av ulike årsaker ikke nådd frem til brukeren)
,   sms.gatewayreference -- referanse hos Link Mobiilty (en ref betyr sannsynligvis at LinkMobility fakturerer for denne - men ikke nødvendigvis)    

     -- takstering (prisgruppe + oppsplitting hos operatøren)
,   CASE 
    WHEN sms.mobilenumber IS NULL THEN 'n/a'
    WHEN sms.mobilenumber ~ '^(\+|00) *47' THEN 'innland' 
    ELSE 'utland' 
END as rate  -- alle nummer som ikke er norske, er definert i takstgruppe utland.   
,   left(sms.mobilenumber, 4) as mobilenumber_prefix --midelritdig felt for å verifisere takst-feltet    
,   sms.smscount as altinn_sms_count -- intern tellelogikk for å bryte opp meldinger over 160 tegn
,   length(sms.customizedbody) as altinn_sms_custom_body_length --antall tegn i meldingen (for meldinger der det er benyttet nøkkelord)
,   length(sms_text.body) as altinn_sms_body_length --antall tegn i tekst mottatt fra bestiller    
     
--,  sms.*, sms_text.*, orders.*, order_chain.*
from notifications.smsnotifications as sms
         inner join notifications.orders orders on orders._id = sms._orderid
         left join notifications.smstexts sms_text on orders._id = sms_text._orderid
    --inner join notifications.orderschain order_chain on orders.alternateid = (order_chain.orderchain ->> 'OrderId')::uuid  
		WHERE orders.requestedsendtime >= start_date
			AND orders.requestedsendtime < start_date + INTERVAL '1 day';
   
END;
$BODY$;

ALTER FUNCTION notifications.get_sms_metrics(integer, integer, integer)
    OWNER TO platform_notifications_admin;
