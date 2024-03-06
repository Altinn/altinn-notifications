drop procedure if exists notifications.insertemailnotification(
IN _orderid uuid,
IN _alternateid uuid,
IN _recipientid text,
IN _toaddress text,
IN _result text,
IN _resulttime timestamp with time zone,
IN _expirytime timestamp with time zone);

drop procedure if exists notifications.insertsmsnotification(
IN _orderid uuid, 
IN _alternateid uuid, 
IN _recipientid text, 
IN _mobilenumber text, 
IN _result text, 
IN _resulttime timestamp with time zone, 
IN _expirytime timestamp with time zone);


CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(
_orderid uuid, 
_alternateid uuid, 
_recipientorgno TEXT,
_recipientnin TEXT,
_toaddress TEXT, 
_result text, 
_resulttime timestamptz, 
_expirytime timestamptz)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN

INSERT INTO notifications.emailnotifications(
_orderid, 
alternateid, 
recipientorgno, 
recipientnin, 
toaddress, result, 
resulttime, 
expirytime)
VALUES (
__orderid, 
_alternateid,
_recipientorgno,
_recipientnin,
_toaddress,
_result::emailnotificationresulttype,
_resulttime,
_expirytime);
END;
$BODY$;


CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(
_orderid uuid, 
_alternateid uuid, 
_recipientorgno TEXT, 
_recipientnin TEXT,
_mobilenumber TEXT, 
_result text, 
_resulttime timestamptz, 
_expirytime timestamptz)
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
resulttime,
expirytime)
VALUES (
__orderid,
_alternateid,
_recipientorgno, 
_recipientnin, 
_mobilenumber,
_result::smsnotificationresulttype,
_resulttime,
_expirytime);
END;
$BODY$;