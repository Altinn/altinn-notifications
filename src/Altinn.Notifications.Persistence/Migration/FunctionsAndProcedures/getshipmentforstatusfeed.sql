CREATE OR REPLACE FUNCTION notifications.getshipmentforstatusfeed(_alternateid uuid)
RETURNS TABLE(
    alternateid uuid,
    reference text,
    status text,
    last_update timestamp with time zone,
    destination text,
    type text
)
LANGUAGE 'plpgsql'
COST 100
VOLATILE PARALLEL UNSAFE
ROWS 1000
AS $BODY$
BEGIN
    RETURN QUERY
    WITH distinct_orders AS (
        SELECT DISTINCT ON (o._id) o.*
        FROM notifications.orders o
        LEFT JOIN notifications.emailnotifications e ON e._orderid = o._id
        LEFT JOIN notifications.smsnotifications s ON s._orderid = o._id
        WHERE e.alternateid = _alternateid OR s.alternateid = _alternateid
    )
    SELECT
        o.alternateid,
        t.reference,      
        t.status,
        t.last_update,
        t.destination,
        t.type
    FROM
        distinct_orders o
        CROSS JOIN LATERAL notifications.get_shipment_tracking_v2(o.alternateid, o.creatorname) AS t;

END;
$BODY$;

ALTER FUNCTION notifications.getshipmentforstatusfeed(uuid)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.getshipmentforstatusfeed(uuid)
    IS 'Retrieves combined order and shipment tracking data based on an email or sms notification alternateid.';


CREATE OR REPLACE FUNCTION notifications.getshipmentforstatusfeed_v2(_alternateid uuid)
RETURNS TABLE(
    alternateid uuid,
    reference text,
    status text,
    last_update timestamp with time zone,
    destination text,
    type text
)
LANGUAGE 'plpgsql'
COST 100
VOLATILE PARALLEL UNSAFE
ROWS 1000
AS $BODY$
DECLARE
    _order_alternateid uuid;
    _order_creatorname text;
BEGIN
    -- First try to find order information in the email notifications table
    SELECT o.alternateid, o.creatorname INTO _order_alternateid, _order_creatorname
    FROM notifications.orders o
    JOIN notifications.emailnotifications e ON e._orderid = o._id
    WHERE e.alternateid = _alternateid
    LIMIT 1;
    
    -- If not found in email notifications, try SMS notifications table
    IF _order_alternateid IS NULL THEN
        SELECT o.alternateid, o.creatorname INTO _order_alternateid, _order_creatorname
        FROM notifications.orders o
        JOIN notifications.smsnotifications s ON s._orderid = o._id
        WHERE s.alternateid = _alternateid
        LIMIT 1;
    END IF;
    
    -- If we found an order, get its tracking information
    IF _order_alternateid IS NOT NULL THEN
        RETURN QUERY
        SELECT
            _order_alternateid,
            t.reference,        
            t.status,
            t.last_update,
            t.destination,
            t.type
        FROM
            notifications.get_shipment_tracking_v2(_order_alternateid, _order_creatorname) AS t;
    END IF;
END;
$BODY$;

ALTER FUNCTION notifications.getshipmentforstatusfeed_v2(uuid)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.getshipmentforstatusfeed_v2(uuid)
    IS 'Retrieves shipment tracking data using an email or sms notification alternateid.';


CREATE OR REPLACE FUNCTION notifications.getshipmentforstatusfeed_v3(_alternateid uuid)
RETURNS TABLE(
    alternateid       uuid,
    reference         text,
    status            text,
    last_update       timestamp with time zone,
    destination       text,
    type              text,
    notification_type text
)
LANGUAGE 'plpgsql'
COST 100
STABLE PARALLEL SAFE
ROWS 5
AS $BODY$
DECLARE
    _order_alternateid uuid;
    _order_creatorname text;
BEGIN
    -- First try to find order information in the email notifications table
    SELECT o.alternateid, o.creatorname INTO _order_alternateid, _order_creatorname
    FROM notifications.orders o
    JOIN notifications.emailnotifications e ON e._orderid = o._id
    WHERE e.alternateid = _alternateid
    LIMIT 1;
    
    -- If not found in email notifications, try SMS notifications table
    IF _order_alternateid IS NULL THEN
        SELECT o.alternateid, o.creatorname INTO _order_alternateid, _order_creatorname
        FROM notifications.orders o
        JOIN notifications.smsnotifications s ON s._orderid = o._id
        WHERE s.alternateid = _alternateid
        LIMIT 1;
    END IF;
    
    -- If we found an order, get its tracking information
    IF _order_alternateid IS NOT NULL THEN
        RETURN QUERY
        SELECT
            _order_alternateid,
            t.reference,        
            t.status,
            t.last_update,
            t.destination,
            t.type,
            t.notification_type 
        FROM
            notifications.get_shipment_tracking_v3(_order_alternateid, _order_creatorname) AS t;
    END IF;
END;
$BODY$;

ALTER FUNCTION notifications.getshipmentforstatusfeed_v3(uuid)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.getshipmentforstatusfeed_v3(uuid)
    IS 'Retrieves shipment tracking data using an email or sms notification alternateid.';
