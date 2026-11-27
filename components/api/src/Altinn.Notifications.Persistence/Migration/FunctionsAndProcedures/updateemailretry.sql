CREATE OR REPLACE PROCEDURE notifications.updateemailretry(
    _alternateid uuid,
    _retryreason text
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    UPDATE notifications.emailnotifications
    SET
        result = 'Retrying'::emailnotificationresulttype,
        resulttime = clock_timestamp(),
        retryreason = _retryreason,
        retrycount = retrycount + 1
    WHERE alternateid = _alternateid;
END;
$BODY$;

COMMENT ON PROCEDURE notifications.updateemailretry(uuid, text) IS
'Sets an email notification to Retrying, records the reason and increments retrycount.
resulttime is reset so that the retry claim functions can apply their retry delay.';
