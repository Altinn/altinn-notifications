CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(__orderid BIGINT, 
																  _alternateid uuid, 
																  _recipientid TEXT, 
																  _toaddress TEXT, 
																  _result emailnotificationresulttype, 
																  _resulttime timestamptz, 
																  _expirytime timestamptz
																 )
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
INSERT INTO notifications.emailnotifications(_orderid, alternateid, recipientid, toaddress, result, resulttime, expirytime)
	VALUES (__orderid, _alternateid, _recipientid, _toaddress, _result, _resulttime, _expirytime);
END;
$BODY$;