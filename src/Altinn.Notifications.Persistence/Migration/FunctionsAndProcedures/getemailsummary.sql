CREATE OR REPLACE FUNCTION notifications.getemailsummary_v2(
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