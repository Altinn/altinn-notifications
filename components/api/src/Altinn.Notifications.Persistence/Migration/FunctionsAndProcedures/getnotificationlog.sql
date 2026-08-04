-- Retrieves notification log entries using either the dialog identifier or the transmission identifier.
CREATE OR REPLACE FUNCTION notifications.getnotificationlog
(
    _dialogid text DEFAULT NULL,
    _transmissionid text DEFAULT NULL
)
RETURNS TABLE (
    notificationid uuid,
    dialogid text,
    transmissionid text,
    type text,
    channel text,
    destination text,
    status text,
    requestedsendtime timestamptz,
    lastupdatetime timestamptz
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF _dialogid IS NOT NULL AND _transmissionid IS NOT NULL THEN
        RETURN QUERY
        SELECT
            nl.notificationid,
            nl.dialogid,
            nl.transmissionid,
            nl.type,
            nl.channel,
            nl.destination,
            nl.status,
            nl.requestedsendtime,
            nl.lastupdatetime
        FROM notifications.notificationlog AS nl
        WHERE nl.dialogid = _dialogid
          AND nl.transmissionid = _transmissionid;

    ELSIF _dialogid IS NOT NULL THEN
        RETURN QUERY
        SELECT
            nl.notificationid,
            nl.dialogid,
            nl.transmissionid,
            nl.type,
            nl.channel,
            nl.destination,
            nl.status,
            nl.requestedsendtime,
            nl.lastupdatetime
        FROM notifications.notificationlog AS nl
        WHERE nl.dialogid = _dialogid;

    ELSIF _transmissionid IS NOT NULL THEN
        RETURN QUERY
        SELECT
            nl.notificationid,
            nl.dialogid,
            nl.transmissionid,
            nl.type,
            nl.channel,
            nl.destination,
            nl.status,
            nl.requestedsendtime,
            nl.lastupdatetime
        FROM notifications.notificationlog AS nl
        WHERE nl.transmissionid = _transmissionid;
    END IF;
END;
$$;

COMMENT ON FUNCTION notifications.getnotificationlog IS
'Retrieves notification log entries using the supported lookup identifiers.

Parameters:
- _dialogid: The Dialogporten dialog identifier used to locate notification log entries.
- _transmissionid: The Dialogporten transmission identifier used to locate notification log entries.';