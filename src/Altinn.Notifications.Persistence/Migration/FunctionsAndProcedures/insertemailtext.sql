CREATE OR REPLACE PROCEDURE notifications.insertemailtext(__orderid BIGINT, _fromaddress TEXT, _subject TEXT, _body TEXT, _contenttype TEXT)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
INSERT INTO notifications.emailtexts(_orderid, fromaddress, subject, body, contenttype)
	VALUES (__orderid, _fromaddress, _subject, _body, _contenttype);
END;
$BODY$;
