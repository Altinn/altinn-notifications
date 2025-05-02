CREATE OR REPLACE FUNCTION notifications.cancelorder(
    _alternateid uuid,
    _creatorname text
)
RETURNS TABLE(
    cancelallowed boolean,
    alternateid uuid,
    creatorname text,
    sendersreference text,
    created timestamp with time zone,                  
    requestedsendtime timestamp with time zone,
    processed timestamp with time zone,
    processedstatus orderprocessingstate,
    notificationchannel text,
    ignorereservation boolean,
    resourceid text,
    conditionendpoint text,
    generatedemailcount bigint,
    succeededemailcount bigint,
    generatedsmscount bigint, 
    succeededsmscount bigint
) 
LANGUAGE plpgsql
AS $$
DECLARE
    order_record RECORD;
BEGIN
    -- Retrieve the order and its status
    SELECT o.requestedsendtime, o.processedstatus
    INTO order_record
    FROM notifications.orders o
    WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname;

    -- If no order is found, return an empty result set
    IF NOT FOUND THEN
        RETURN;
    END IF;
    
     -- Check if order is already cancelled
     IF order_record.processedstatus = 'Cancelled' THEN
        RETURN QUERY 
        SELECT TRUE AS cancelallowed,
           order_details.*
        FROM notifications.getorder_includestatus_v4(_alternateid, _creatorname) AS order_details;
     ELSIF (order_record.requestedsendtime <= NOW() + INTERVAL '5 minutes' or order_record.processedstatus != 'Registered') THEN
        RETURN QUERY 
        SELECT FALSE AS cancelallowed, NULL::uuid, NULL::text, NULL::text, NULL::timestamp with time zone, NULL::timestamp with time zone, NULL::timestamp with time zone, NULL::orderprocessingstate, NULL::text, NULL::boolean, NULL::text, NULL::text, NULL::bigint, NULL::bigint, NULL::bigint, NULL::bigint;
     ELSE 
        -- Cancel the order by updating its status
        UPDATE notifications.orders o
        SET processedstatus = 'Cancelled', processed = NOW()
        WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname;

        -- Retrieve the updated order details
        RETURN QUERY 
        SELECT TRUE AS cancelallowed,
               order_details.*
        FROM notifications.getorder_includestatus_v4(_alternateid, _creatorname) AS order_details;
    END IF;      
END;
$$;
