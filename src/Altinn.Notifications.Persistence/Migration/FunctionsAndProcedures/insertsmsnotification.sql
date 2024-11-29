CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(
    _orderid uuid, 
    _alternateid uuid, 
    _recipientorgno TEXT, 
    _recipientnin TEXT,
    _mobilenumber TEXT, 
    _customizedbody TEXT,
    _result text, 
    _smscount integer,
    _resulttime timestamptz, 
    _expirytime timestamptz
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    __orderid BIGINT;
BEGIN
    SELECT _id INTO __orderid 
    FROM notifications.orders
    WHERE alternateid = _orderid;

    INSERT INTO notifications.smsnotifications(
        _orderid, 
        alternateid,
        recipientorgno, 
        recipientnin, 
        mobilenumber,
        customizedbody,
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
        _customizedbody,
        _result::smsnotificationresulttype,
        _smscount,
        _resulttime,
        _expirytime);
END;
$BODY$;