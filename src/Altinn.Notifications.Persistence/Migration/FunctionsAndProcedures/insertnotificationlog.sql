CREATE OR REPLACE FUNCTION notifications.insert_notification_log(
    _shipmentid uuid,
    _type notificationordertype,
    _orderchainid bigint DEFAULT NULL,
    _dialogid uuid DEFAULT NULL,
    _transmissionid text DEFAULT NULL,
    _operationid text DEFAULT NULL,
    _gatewayreference text DEFAULT NULL,
    _recipient text DEFAULT NULL,
    _destination text DEFAULT NULL,
    _resource text DEFAULT NULL,
    _status text DEFAULT NULL,
    _sent_timestamp timestamp with time zone DEFAULT NULL
)
RETURNS bigint
LANGUAGE plpgsql
VOLATILE PARALLEL UNSAFE
AS $$
DECLARE
    new_id bigint;
BEGIN
    INSERT INTO notifications.notificationlog (
        orderchainid,
        shipmentid,
        dialogid,
        transmissionid,
        operationid,
        gatewayreference,
        recipient,
        type,
        destination,
        resource,
        status,
        sent_timestamp
    ) VALUES (
        _orderchainid,
        _shipmentid,
        _dialogid,
        _transmissionid,
        _operationid,
        _gatewayreference,
        _recipient,
        _type,
        _destination,
        _resource,
        _status,
        _sent_timestamp
    ) RETURNING id INTO new_id;
    
    RETURN new_id;
END;
$$;

COMMENT ON FUNCTION notifications.insert_notification_log IS
'Inserts a new notification log entry and returns the generated ID.
Required parameters: _shipmentid, _type
Optional parameters: all others (will use DEFAULT values if not provided)
Returns: The auto-generated ID of the inserted row';
