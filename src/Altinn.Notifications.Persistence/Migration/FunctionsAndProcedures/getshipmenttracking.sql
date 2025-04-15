CREATE OR REPLACE FUNCTION notifications.get_shipment_tracking(
_alternateid UUID,
_creatorname TEXT)
RETURNS TABLE (
    reference     TEXT,
    status        TEXT,
    last_update   TIMESTAMPTZ,
    destination   TEXT
) AS $$
DECLARE
    v_order_exists BOOLEAN;
BEGIN
    -- Check if the order exists and store the result
    SELECT EXISTS (
        SELECT 1
        FROM notifications.orders o
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname
    ) INTO v_order_exists;
    
    -- Exit early if the order doesn't exist
    IF NOT v_order_exists THEN
        RETURN;
    END IF;

    -- Return shipment tracking results
    RETURN QUERY
    WITH order_data AS (
        SELECT o._id, o.sendersreference, o.created, o.processed, o.processedstatus
        FROM notifications.orders o
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname
    ),
    order_tracking AS (
        SELECT
            od.sendersreference AS reference,
            od.processedstatus::TEXT AS status,
            GREATEST(od.created, COALESCE(od.processed, od.created)) AS last_update,
            NULL::TEXT AS destination
        FROM order_data od
    ),
    email_tracking AS (
        SELECT
            NULL::TEXT AS reference,
            e.result::TEXT AS status,
            e.resulttime AS last_update,
            e.toaddress AS destination
        FROM order_data od
        JOIN notifications.emailnotifications e ON e._orderid = od._id
    ),
    sms_tracking AS (
        SELECT
            NULL::TEXT AS reference,
            s.result::TEXT AS status,
            s.resulttime AS last_update,
            s.mobilenumber AS destination
        FROM order_data od
        JOIN notifications.smsnotifications s ON s._orderid = od._id
    )
    SELECT * FROM order_tracking
    UNION ALL
    SELECT * FROM email_tracking
    UNION ALL
    SELECT * FROM sms_tracking;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION notifications.get_shipment_tracking(UUID, TEXT) IS
'Returns tracking information for a notification shipment identified by the given alternate identifier and creator name.
Includes:
 - Order-level status and reference information
 - Associated delivery via email and SMS channels
Results are returned in a single table.
If no matching order exists, an empty result set is returned.';
