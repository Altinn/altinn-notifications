DROP FUNCTION IF EXISTS notifications.getsms_statusnew_updatestatus(_sendingtimepolicy integer);

DROP FUNCTION IF EXISTS notifications.getsms_statusnew_updatestatus();

DROP PROCEDURE IF EXISTS notifications.updateemailstatus(IN _alternateid uuid, IN _result text, IN _operationid text);
