CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(_orderid uuid, 
																  _alternateid uuid, 
																  _recipientid TEXT, 
																  _mobilenumber TEXT, 
																  _result text, 
																  _resulttime timestamptz, 
																  _expirytime timestamptz
																 )
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN

INSERT INTO notifications.smsnotifications(_orderid, alternateid, recipientid, mobilenumber, result, resulttime, expirytime)
	VALUES (__orderid, _alternateid, _recipientid, _mobilenumber, _result::smsnotificationresulttype, _resulttime, _expirytime);
END;
$BODY$