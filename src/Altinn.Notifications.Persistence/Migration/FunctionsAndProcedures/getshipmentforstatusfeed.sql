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