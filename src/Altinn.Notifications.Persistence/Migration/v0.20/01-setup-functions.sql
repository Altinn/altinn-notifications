
CREATE OR REPLACE FUNCTION notifications.getorder_includestatus_v2(
	_alternateid uuid,
	_creatorname text)
    RETURNS TABLE(alternateid uuid,
	creatorname text,
	sendersreference text,
	created timestamp with time zone,				  
	requestedsendtime timestamp with time zone,
	processed timestamp with time zone,
	processedstatus orderprocessingstate,
	notificationchannel text,
	generatedemailcount bigint,
	succeededemailcount bigint,
	generatedsmscount bigint, 
	succeededsmscount bigint) 
    LANGUAGE 'plpgsql'
AS $BODY$

DECLARE
    _target_orderid INTEGER;
    _succeededEmailCount BIGINT;
    _generatedEmailCount BIGINT;
	_succeededSmsCount BIGINT;
    _generatedSmsCount BIGINT;
BEGIN
    SELECT _id INTO _target_orderid FROM notifications.orders
        WHERE orders.alternateid = _alternateid AND orders.creatorname = _creatorname;
	
    SELECT
	  SUM(CASE WHEN result = 'Succeeded' THEN 1 ELSE 0 END), COUNT(1) AS generatedEmailCount
        INTO _succeededEmailCount, _generatedEmailCount
        FROM notifications.emailnotifications
        WHERE _orderid = _target_orderid;
    
	SELECT		
	SUM(CASE WHEN result = 'Accepted' THEN 1 ELSE 0 END), COUNT(1) AS generatedSmsCount
        INTO _succeededSmsCount, _generatedSmsCount
        FROM notifications.smsnotifications
        WHERE _orderid = _target_orderid;

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
        _generatedEmailCount,
        _succeededEmailCount,
		_generatedSmsCount, 
		_succeededSmsCount
    FROM
        notifications.orders AS orders
    WHERE 
        orders.alternateid = _alternateid;
END;
$BODY$;
