CREATE OR REPLACE FUNCTION notifications.getshipmentforstatusfeed_v3(_alternateid uuid)
RETURNS TABLE(
    alternateid       uuid,
    reference         text,
    status            text,
    last_update       timestamp with time zone,
    destination       text,
    type              text
)
LANGUAGE sql
STABLE
PARALLEL SAFE
STRICT
ROWS 5
AS $$
WITH picked_order AS (
    (
        SELECT o.alternateid, o.creatorname
        FROM notifications.emailnotifications e
        JOIN notifications.orders o ON o._id = e._orderid
        WHERE e.alternateid = _alternateid

        UNION ALL

        SELECT o.alternateid, o.creatorname
        FROM notifications.smsnotifications s
        JOIN notifications.orders o ON o._id = s._orderid
        WHERE s.alternateid = _alternateid
    )
    LIMIT 1
)
SELECT
    p.alternateid,
    t.reference,
    t.status,
    t.last_update,
    t.destination,
    t.type
FROM picked_order p
CROSS JOIN LATERAL notifications.get_shipment_tracking_v3(p.alternateid, p.creatorname) AS t;
$$;

ALTER FUNCTION notifications.getshipmentforstatusfeed_v3(uuid)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.getshipmentforstatusfeed_v3(uuid)
    IS 'Retrieves shipment tracking data using an email or sms notification alternateid.';
