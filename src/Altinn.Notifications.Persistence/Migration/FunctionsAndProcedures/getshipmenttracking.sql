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
    -- Check for the existence of the order
    SELECT EXISTS (
        SELECT 1
        FROM notifications.orders o
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname
    )
    INTO v_order_exists;

    -- Return empty set if no order is found
    IF NOT v_order_exists THEN
        RETURN;
    END IF;

    -- Return combined tracking info
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
            od.sendersreference AS reference,
            e.result::TEXT AS status,
            e.resulttime AS last_update,
            e.toaddress AS destination
        FROM order_data od
        JOIN notifications.emailnotifications e ON e._orderid = od._id
    ),
    sms_tracking AS (
        SELECT
            od.sendersreference AS reference,
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
'Returns delivery tracking information for a notification identified by the given alternate identifier and creator name.

Includes:
 - Order-level tracking (reference and status)
 - Email notification tracking (status, result time, destination)
 - SMS notification tracking (status, result time, destination)

If no matching order exists, an empty result set is returned.';


-- This is a new version to return the type of the notification
CREATE OR REPLACE FUNCTION notifications.get_shipment_tracking_v3(
    _alternateid UUID,
    _creatorname TEXT)
RETURNS TABLE (
    reference          TEXT,
    status             TEXT,
    last_update        TIMESTAMPTZ,
    destination        TEXT,
    type               TEXT,
    notification_type  TEXT
) AS $$
DECLARE
    v_order_exists BOOLEAN;
BEGIN
    -- Check for the existence of the order
    SELECT EXISTS (
        SELECT 1
        FROM notifications.orders o
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname
    )
    INTO v_order_exists;

    -- Return empty set if no order is found
    IF NOT v_order_exists THEN
        RETURN;
    END IF;

    -- Return combined tracking info
    RETURN QUERY
    WITH order_data AS (
        SELECT o._id, o.sendersreference, o.created, o.processed, o.processedstatus, o.type
        FROM notifications.orders o
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname
    ),
    order_tracking AS (
        SELECT
            od.sendersreference AS reference,
            od.processedstatus::TEXT AS status,
            GREATEST(od.created, COALESCE(od.processed, od.created)) AS last_update,
            NULL::TEXT AS destination,
            od.type::TEXT AS type,
            'order' AS notification_type
        FROM order_data od
    ),
    email_tracking AS (
        SELECT
            od.sendersreference AS reference,
            e.result::TEXT AS status,
            e.resulttime AS last_update,
            e.toaddress AS destination,
            od.type::TEXT AS type,
            'email' AS notification_type
        FROM order_data od
        JOIN notifications.emailnotifications e ON e._orderid = od._id
    ),
    sms_tracking AS (
        SELECT
            od.sendersreference AS reference,
            s.result::TEXT AS status,
            s.resulttime AS last_update,
            s.mobilenumber AS destination,
            od.type::TEXT AS type,
            'sms' AS notification_type
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

COMMENT ON FUNCTION notifications.get_shipment_tracking_v3(UUID, TEXT) IS
'Returns delivery tracking information for a notification identified by the given alternate identifier and creator name.

Includes:
 - Order-level tracking (reference and status)
 - Email notification tracking (status, result time, destination)
 - SMS notification tracking (status, result time, destination)

If no matching order exists, an empty result set is returned.';
