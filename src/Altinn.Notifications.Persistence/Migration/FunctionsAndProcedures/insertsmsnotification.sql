CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(
_orderid uuid, 
_alternateid uuid, 
_recipientorgno TEXT, 
_recipientnin TEXT,
_mobilenumber TEXT, 
_result text, 
_smscount integer,
_resulttime timestamptz, 
_expirytime timestamptz
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN

INSERT INTO notifications.smsnotifications(
_orderid, 
alternateid,
recipientorgno, 
recipientnin, 
mobilenumber,
result,
smscount,
resulttime,
expirytime)
VALUES (
__orderid,
_alternateid,
_recipientorgno, 
_recipientnin, 
_mobilenumber,
_result::smsnotificationresulttype,
_smscount,
_resulttime,
_expirytime);
END;
$BODY$;