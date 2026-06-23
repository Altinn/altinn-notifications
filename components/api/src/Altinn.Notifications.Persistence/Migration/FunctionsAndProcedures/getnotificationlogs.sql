CREATE OR REPLACE FUNCTION notifications.get_notification_logs(
    _id   TEXT,
    _id_type TEXT  -- 'shipmentid' | 'dialogid' | 'transmissionid'
)
RETURNS TABLE (
    id               bigint,
    orderchainid     int8,
    shipmentid       uuid,
    creatorname      text,
    dialogid         text,
    transmissionid   text,
    operationid      text,
    gatewayreference text,
    recipient        text,
    type             notificationordertype,
    destination      text,
    resource         text,
    status           text,
    created_timestamp timestamptz,
    last_update_timestamp   timestamptz
)
LANGUAGE plpgsql
STABLE PARALLEL SAFE
AS $$
BEGIN
    IF _id_type = 'shipmentid' THEN
        RETURN QUERY
        SELECT
            nl.id,
            nl.orderchainid,
            nl.shipmentid,
            nl.creatorname,
            nl.dialogid,
            nl.transmissionid,
            nl.operationid,
            nl.gatewayreference,
            nl.recipient,
            nl.type,
            nl.destination,
            nl.resource,
            nl.status,
            nl.created_timestamp,
            nl.last_update_timestamp
        FROM notifications.notificationlog nl
        WHERE nl.shipmentid = _id::uuid;

    ELSIF _id_type = 'dialogid' THEN
        RETURN QUERY
        SELECT
            nl.id,
            nl.orderchainid,
            nl.shipmentid,
            nl.creatorname,
            nl.dialogid,
            nl.transmissionid,
            nl.operationid,
            nl.gatewayreference,
            nl.recipient,
            nl.type,
            nl.destination,
            nl.resource,
            nl.status,
            nl.created_timestamp,
            nl.last_update_timestamp
        FROM notifications.notificationlog nl
        WHERE nl.dialogid = _id;

    ELSIF _id_type = 'transmissionid' THEN
        RETURN QUERY
        SELECT
            nl.id,
            nl.orderchainid,
            nl.shipmentid,
            nl.creatorname,
            nl.dialogid,
            nl.transmissionid,
            nl.operationid,
            nl.gatewayreference,
            nl.recipient,
            nl.type,
            nl.destination,
            nl.resource,
            nl.status,
            nl.created_timestamp,
            nl.last_update_timestamp
        FROM notifications.notificationlog nl
        WHERE nl.transmissionid = _id;

    ELSE
        RAISE EXCEPTION 'Invalid _id_type: %. Must be shipmentid, dialogid or transmissionid.', _id_type;
    END IF;
END;
$$;

COMMENT ON FUNCTION notifications.get_notification_logs(text, text) IS
'Returns notification log entries for a given ID. The _id_type parameter controls which column is matched:
  - ''shipmentid''     : matches notificationlog.shipmentid (uuid)
  - ''dialogid''       : matches notificationlog.dialogid (text)
  - ''transmissionid'' : matches notificationlog.transmissionid (text)
Returns all columns of notificationlog. Raises an exception for unknown _id_type values.';
