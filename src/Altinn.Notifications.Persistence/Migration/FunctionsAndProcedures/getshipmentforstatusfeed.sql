CREATE OR REPLACE FUNCTION notifications.getshipmentforstatusfeed(
    _alternateid UUID
)
RETURNS TABLE (
    alternateid UUID,
    reference TEXT,
    status TEXT,
    last_update TIMESTAMPTZ,
    destination TEXT,
    type TEXT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.alternateid,
        tracking_data.*
    FROM
        notifications.orders o
    -- Use LEFT JOIN to find orders that might have only one type of notification
    LEFT JOIN
        notifications.emailnotifications e ON e._orderid = o._id
    LEFT JOIN
        notifications.smsnotifications s ON s._orderid = o._id
    -- The LATERAL join calls the function for each row produced by the join above.
    CROSS JOIN LATERAL
        notifications.get_shipment_tracking_v2(o.alternateid, o.creatorname) AS tracking_data
    WHERE
        -- This WHERE clause now correctly filters the results of the LEFT JOINs
        e.alternateid = _alternateid OR s.alternateid = _alternateid;

END;
$$ LANGUAGE plpgsql SECURITY INVOKER;

COMMENT ON FUNCTION notifications.getshipmentforstatusfeed(UUID) IS 'Retrieves combined order and shipment tracking data based on an email or sms notification alternateid.';
