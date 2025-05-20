CREATE OR REPLACE FUNCTION notifications.trymarkorderascompleted(notificationid uuid)
RETURNS boolean AS $$
DECLARE
    order_id bigint;
    order_status orderprocessingstate;
    has_pending_notifications boolean := false;
BEGIN
    -- First, try to find order_id from the SMS notifications table
    SELECT _orderid INTO order_id 
    FROM notifications.smsnotifications 
    WHERE alternateid = notificationid;
    
    -- If not found, try the Email notifications table
    IF order_id IS NULL THEN
        SELECT _orderid INTO order_id 
        FROM notifications.emailnotifications 
        WHERE alternateid = notificationid;
    END IF;
               
    -- If order_id is not found, return false (invalid notification identifier)
     IF order_id IS NULL THEN
         RETURN false;
     END IF;
    
    -- Check if order is already completed
    SELECT processedstatus INTO order_status
    FROM notifications.orders
    WHERE _id = order_id
    FOR UPDATE; -- This locks the row until transaction ends
    
    -- If order is already completed, return false (no change needed)
    IF order_status = 'Completed'::orderprocessingstate THEN
        RETURN false;
    END IF;
    
    -- Check if any SMS or Email notifications are still pending
    SELECT EXISTS(
        SELECT 1 
        FROM notifications.smsnotifications 
        WHERE _orderid = order_id 
        AND result::TEXT IN ('New', 'Sending', 'Accepted')
        
        UNION
        
        SELECT 1
        FROM notifications.emailnotifications 
        WHERE _orderid = order_id 
        AND result::TEXT IN ('New', 'Sending', 'Succeeded')
    ) INTO has_pending_notifications;
    
    -- If any notifications still pending, return false (no change needed)
    IF has_pending_notifications THEN
        RETURN false;
    END IF;
    
    -- No pending Email and SMS notifications, update order status to Completed
    UPDATE notifications.orders
    SET processedstatus = 'Completed'::orderprocessingstate,
        processed = now()
    WHERE _id = order_id;
    
    RETURN true;
END;
$$ LANGUAGE plpgsql;