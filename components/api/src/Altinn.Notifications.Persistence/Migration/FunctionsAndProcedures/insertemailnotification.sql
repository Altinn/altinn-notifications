CREATE OR REPLACE PROCEDURE notifications.insertemailnotification_v2(
    _orderid uuid,
    _alternateid uuid,
    _recipientorgno TEXT,
    _recipientnin TEXT,
    _toaddress TEXT,
    _customizedbody TEXT,
    _customizedsubject TEXT,
    _result TEXT,
    _resulttime timestamptz,
    _expirytime timestamptz,
    _total_attachment_size_bytes BIGINT
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    __orderid BIGINT;
BEGIN
    SELECT _id INTO __orderid
    FROM notifications.orders
    WHERE alternateid = _orderid;

    INSERT INTO notifications.emailnotifications(
        _orderid,
        alternateid,
        recipientorgno,
        recipientnin,
        toaddress,
        customizedbody,
        customizedsubject,
        result,
        resulttime,
        expirytime,
        total_attachment_size_bytes
    )
    VALUES (
        __orderid,
        _alternateid,
        _recipientorgno,
        _recipientnin,
        _toaddress,
        _customizedbody,
        _customizedsubject,
        _result::emailnotificationresulttype,
        _resulttime,
        _expirytime,
        _total_attachment_size_bytes
    );
END;
$BODY$;

ALTER PROCEDURE notifications.insertemailnotification_v2(uuid, uuid, text, text, text, text, text, text, timestamptz, timestamptz, bigint)
    OWNER TO platform_notifications_admin;

COMMENT ON PROCEDURE notifications.insertemailnotification_v2(uuid, uuid, text, text, text, text, text, text, timestamptz, timestamptz, bigint)
    IS 'Inserts a new email notification linked to the given order.
Raises an exception if no order with the specified alternateid exists.
_total_attachment_size_bytes: total raw attachment size in bytes (0 for standard emails with no attachments).';
