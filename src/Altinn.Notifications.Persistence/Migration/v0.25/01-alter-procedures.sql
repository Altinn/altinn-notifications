CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(_orderid uuid, 
																  _alternateid uuid, 
																  _toaddress TEXT, 
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

INSERT INTO notifications.emailnotifications(_orderid, alternateid,  toaddress, result, resulttime, expirytime)
	VALUES (__orderid, _alternateid, _toaddress, _result::emailnotificationresulttype, _resulttime, _expirytime);
END;
$BODY$;


CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(_orderid uuid, 
																  _alternateid uuid, 
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

INSERT INTO notifications.smsnotifications(_orderid, alternateid, mobilenumber, result, resulttime, expirytime)
	VALUES (__orderid, _alternateid, _mobilenumber, _result::smsnotificationresulttype, _resulttime, _expirytime);
END;
$BODY$