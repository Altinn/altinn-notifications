CREATE OR REPLACE FUNCTION notifications.insertorder_v2(
    _alternateid uuid,
    _creatorname text,
    _sendersreference text,
    _created timestamp with time zone,
    _requestedsendtime timestamp with time zone,
    _notificationorder jsonb,
    _sendingtimepolicy integer,
    _type text,
    _processingstatus text,
    _orderchainid bigint
)
RETURNS bigint
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _orderid BIGINT;
BEGIN
    INSERT INTO notifications.orders(
        alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder, smssendingtimepolicy, type, processedstatus, _orderchainid
    )
    VALUES (
        _alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder, _sendingtimepolicy, _type::public.notificationordertype, _processingstatus::public.orderprocessingstate, _orderchainid
    )
    RETURNING _id INTO _orderid;

    RETURN _orderid;
END;
$BODY$;


CREATE OR REPLACE FUNCTION notifications.insertorder_v3(
    _alternateid uuid,
    _creatorname text,
    _sendersreference text,
    _created timestamp with time zone,
    _requestedsendtime timestamp with time zone,
    _notificationorder jsonb,
    _smssendingtimepolicy integer,
    _type text,
    _processingstatus text,
    _orderchainid bigint,
    _emailsendingtimepolicy integer
)
RETURNS bigint
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _orderid BIGINT;
BEGIN
    INSERT INTO notifications.orders(
        alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder, smssendingtimepolicy, type, processedstatus, _orderchainid, emailsendingtimepolicy
    )
    VALUES (
        _alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder, _smssendingtimepolicy, _type::public.notificationordertype, _processingstatus::public.orderprocessingstate, _orderchainid, _emailsendingtimepolicy
    )
    RETURNING _id INTO _orderid;

    RETURN _orderid;
END;
$BODY$;
