CREATE OR REPLACE FUNCTION notifications.getorder_includestatus_v4(
    _alternateid uuid,
    _creatorname text
)
RETURNS TABLE(
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
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _target_orderid INTEGER;
    _succeededEmailCount BIGINT;
    _generatedEmailCount BIGINT;
    _succeededSmsCount BIGINT;
    _generatedSmsCount BIGINT;
BEGIN
    SELECT _id INTO _target_orderid 
    FROM notifications.orders
    WHERE orders.alternateid = _alternateid 
    AND orders.creatorname = _creatorname;
    
    SELECT
        SUM(CASE WHEN result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END), 
        COUNT(1) AS generatedEmailCount
    INTO _succeededEmailCount, _generatedEmailCount
    FROM notifications.emailnotifications
    WHERE emailnotifications._orderid = _target_orderid;
    
    SELECT      
        SUM(CASE WHEN result = 'Accepted' THEN 1 ELSE 0 END), 
        COUNT(1) AS generatedSmsCount
    INTO _succeededSmsCount, _generatedSmsCount
    FROM notifications.smsnotifications
    WHERE smsnotifications._orderid = _target_orderid;

    RETURN QUERY
    SELECT 
        orders.alternateid,
        orders.creatorname,
        orders.sendersreference,
        orders.created,
        orders.requestedsendtime,
        orders.processed,
        orders.processedstatus,
        orders.notificationorder->>'NotificationChannel',
        CASE 
            WHEN orders.notificationorder->>'IgnoreReservation' IS NULL THEN NULL
            ELSE (orders.notificationorder->>'IgnoreReservation')::BOOLEAN
        END AS IgnoreReservation,
        orders.notificationorder->>'ResourceId',
        orders.notificationorder->>'ConditionEndpoint',
        _generatedEmailCount,
        _succeededEmailCount,
        _generatedSmsCount, 
        _succeededSmsCount
    FROM
        notifications.orders AS orders
    WHERE 
        orders._id = _target_orderid;
END;
$BODY$;
