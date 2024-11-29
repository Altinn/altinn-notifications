DROP PROCEDURE IF EXISTS notifications.insertemailnotification(
    IN _orderid uuid,
    IN _alternateid uuid,
    IN _recipientorgno TEXT,
    IN _recipientnin TEXT,
    IN _toaddress TEXT,
    IN _result TEXT,
    IN _resulttime timestamptz,
    IN _expirytime timestamptz
);

DROP PROCEDURE IF EXISTS notifications.insertsmsnotification(
    IN _orderid uuid,
    IN _alternateid uuid,
    IN _recipientorgno TEXT,
    IN _recipientnin TEXT,
    IN _mobilenumber TEXT,
    IN _result TEXT,
    IN _smscount integer,
    IN _resulttime timestamptz,
    IN _expirytime timestamptz
);

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

    INSERT INTO notifications.emailnotifications(
        _orderid,
        alternateid,
        recipientorgno,
        recipientnin,
        toaddress,
        customizedbody,
        customizedsubject,
        result,
        resulttime,
        expirytime
    )
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
        _expirytime
    );
END;
$BODY$;

CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(
    _orderid uuid,
    _alternateid uuid,
    _recipientorgno TEXT,
    _recipientnin TEXT,
    _mobilenumber TEXT,
    _customizedbody TEXT,
    _result TEXT,
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
        expirytime
    )
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
        _expirytime
    );
END;
$BODY$;