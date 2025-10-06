-- Create function for updating email notification status
-- Issue #1050: Intermittent 30s+ timeout in EmailNotificationRepository.UpdateSendStatus
-- This consolidates the conditional logic for updating by alternateid or operationid
-- Replaces separate UPDATE queries in EmailNotificationRepository.UpdateSendStatus

CREATE OR REPLACE FUNCTION notifications.updateemailnotification(
    _result emailnotificationresulttype,
    _operationid text,
    _alternateid uuid
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_alternateid uuid;
BEGIN
    IF _alternateid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result,
            resulttime = now(),
            operationid = COALESCE(_operationid, operationid)
        WHERE alternateid = _alternateid
        RETURNING alternateid INTO v_alternateid;

    ELSIF _operationid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result,
            resulttime = now()
        WHERE operationid = _operationid
        RETURNING alternateid INTO v_alternateid;
    END IF;

    RETURN v_alternateid; -- null => not found
END;
$$;