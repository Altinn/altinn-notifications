CREATE OR REPLACE PROCEDURE notifications.updateemailstatus(_alternateid UUID, _result text, _operationid text)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
	UPDATE notifications.emailnotifications 
	SET result = _result::emailnotificationresulttype, resulttime = now(), operationid = _operationid
	WHERE alternateid = _alternateid;
END;
$BODY$;