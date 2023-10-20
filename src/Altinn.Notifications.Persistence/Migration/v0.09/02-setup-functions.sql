DROP FUNCTION IF EXISTS notifications.getorder_includestatus(_alternateid UUID, _creatorname TEXT);
CREATE OR REPLACE FUNCTION notifications.getorder_includestatus(_alternateid UUID, _creatorname TEXT)
RETURNS TABLE (
    alternateid UUID,
    creatorname TEXT,
    sendersreference TEXT,
    created TIMESTAMPTZ,
    requestedsendtime TIMESTAMPTZ,
    processed TIMESTAMPTZ,
    processedstatus orderprocessingstate,
    generatedEmailCount BIGINT,
    succeededEmailCount BIGINT
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _target_orderid INTEGER;
    _succeededEmailCount BIGINT;
    _generatedEmailCount BIGINT;
BEGIN
    SELECT _id INTO _target_orderid FROM notifications.orders
        WHERE orders.alternateid = _alternateid AND orders.creatorname = _creatorname;

    SELECT
        SUM(CASE WHEN result = 'Succeeded' THEN 1 ELSE 0 END), COUNT(1) AS generatedEmailCount
        INTO _succeededEmailCount, _generatedEmailCount
        FROM notifications.emailnotifications
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
        _generatedEmailCount,
        _succeededEmailCount
    FROM
        notifications.orders AS orders
    WHERE 
        orders.alternateid = _alternateid;
END;
$BODY$;