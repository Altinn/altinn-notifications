CREATE OR REPLACE PROCEDURE notifications.updatesmsretry(
    _alternateid uuid,
    _retryreason text
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    UPDATE notifications.smsnotifications
    SET
        result = 'Retrying'::smsnotificationresulttype,
        resulttime = now(),
        retryreason = _retryreason,
        retrycount = retrycount + 1
    WHERE alternateid = _alternateid;
END;
$BODY$;

COMMENT ON PROCEDURE notifications.updatesmsretry(uuid, text) IS
'Sets an SMS notification to Retrying, records the reason and increments retrycount.
resulttime is reset so that the retry claim functions can apply their retry delay.';
