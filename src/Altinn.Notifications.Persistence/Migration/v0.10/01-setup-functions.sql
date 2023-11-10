CREATE OR REPLACE FUNCTION notifications.getemailsummary(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientid text, 
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
		   SELECT o.sendersreference, n.alternateid, n.recipientid, n.toaddress, n.result, n.resulttime
			FROM notifications.emailnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::emailnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;
