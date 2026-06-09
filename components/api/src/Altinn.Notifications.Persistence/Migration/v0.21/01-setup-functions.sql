CREATE OR REPLACE FUNCTION notifications.getsmssummary(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientid text, 
        mobilenumber text, 
        result smsnotificationresulttype, 
        resulttime timestamptz) 
    LANGUAGE 'plpgsql'
AS $BODY$

	BEGIN
		RETURN QUERY
		   SELECT o.sendersreference, n.alternateid, n.recipientid, n.mobilenumber, n.result, n.resulttime
			FROM notifications.smsnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::smsnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;
