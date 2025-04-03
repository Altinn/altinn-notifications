CREATE OR REPLACE FUNCTION notifications.insertorder(_alternateid UUID, _creatorname TEXT, _sendersreference TEXT, _created TIMESTAMPTZ, _requestedsendtime TIMESTAMPTZ, _notificationorder JSONB)
RETURNS BIGINT
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;

-- Postgres supports function overloading - so this function has the same name, but one additional parameter: sendingTimePolicy
CREATE OR REPLACE FUNCTION notifications.insertorder(
	_alternateid uuid,
	_creatorname text,
	_sendersreference text,
	_created timestamp with time zone,
	_requestedsendtime timestamp with time zone,
	_notificationorder jsonb,
	_sendingtimepolicy integer)
    RETURNS bigint
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$

DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder, sendingtimepolicy) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder, _sendingtimepolicy)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;

ALTER FUNCTION notifications.insertorder(uuid, text, text, timestamp with time zone, timestamp with time zone, jsonb, integer)
    OWNER TO postgres;
