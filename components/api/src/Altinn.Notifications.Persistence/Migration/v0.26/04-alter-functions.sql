CREATE OR REPLACE FUNCTION notifications.getsmsrecipients_v2(_orderid uuid)
RETURNS TABLE(
  recipientorgno text, 
  recipientnin text,
  mobilenumber text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN
RETURN query 
	SELECT s.recipientorgno, s.recipientnin, s.mobilenumber
	FROM notifications.smsnotifications s
	WHERE s._orderid = __orderid;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getemailrecipients_v2(_alternateid uuid)
RETURNS TABLE(
    recipientorgno text, 
    recipientnin text, 
    toaddress text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _alternateid);
BEGIN
RETURN query 
	SELECT e.recipientorgno, e.recipientnin, e.toaddress
	FROM notifications.emailnotifications e
	WHERE e._orderid = __orderid;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getsmssummary_v2(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientorgno text, 
        recipientnin text, 
        mobilenumber text, 
        result smsnotificationresulttype, 
        resulttime timestamptz) 
    LANGUAGE 'plpgsql'
AS $BODY$

	BEGIN
		RETURN QUERY
		   SELECT o.sendersreference, n.alternateid, n.recipientorgno, n.recipientnin, n.mobilenumber, n.result, n.resulttime
			FROM notifications.smsnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::text, NULL::smsnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getemailsummary_V2(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientorgno text, 
        recipientnin text, 
        toaddress text, 
        result emailnotificationresulttype, 
        resulttime timestamptz) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$

	BEGIN
		RETURN QUERY
		   SELECT o.sendersreference, n.alternateid, n.recipientorgno, n.recipientnin, n.toaddress, n.result, n.resulttime
			FROM notifications.emailnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::text, NULL::emailnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;