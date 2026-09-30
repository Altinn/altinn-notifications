CREATE OR REPLACE PROCEDURE notifications.updateorderretry(
    _alternateid uuid,
    _retryreason TEXT,
    _maxretrycount INTEGER
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    UPDATE notifications.orders
    SET
        retryreason = _retryreason,
        processed = CURRENT_TIMESTAMP,
        retrycount = retrycount + 1,
        processedstatus = CASE
        WHEN retrycount + 1 > _maxretrycount THEN 'Failed'::orderprocessingstate
        ELSE 'Retrying'::orderprocessingstate
        END
    WHERE alternateid = _alternateid;
END;
$BODY$;
