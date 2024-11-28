CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(
_orderid uuid,
_alternateid uuid,
_recipientorgno TEXT,
_recipientnin TEXT,
_toaddress TEXT,
_customizedbody TEXT,
_customizedsubject TEXT,
_result TEXT,
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
toaddress,
customizedBody,
customizedSubject,
result,
resulttime,
expirytime)
VALUES (
__orderid,
_alternateid,
_recipientorgno,
_recipientnin,
_toaddress,
_customizedbody,
_customizedsubject,
_result::emailnotificationresulttype,
_resulttime,
_expirytime);
END;
$BODY$;