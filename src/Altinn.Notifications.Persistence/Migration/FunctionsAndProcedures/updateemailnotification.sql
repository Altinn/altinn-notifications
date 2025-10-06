-- Create function for updating email notification status
-- Issue #1050: Intermittent 30s+ timeout in EmailNotificationRepository.UpdateSendStatus
-- This consolidates the conditional logic for updating by alternateid or operationid
-- Replaces separate UPDATE queries in EmailNotificationRepository.UpdateSendStatus

CREATE OR REPLACE FUNCTION notifications.updateemailnotification(
    _result text,
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
        SET result = _result::emailnotificationresulttype,
            resulttime = now(),
            operationid = COALESCE(_operationid, operationid)
        WHERE alternateid = _alternateid
        RETURNING alternateid INTO v_alternateid;

    ELSIF _operationid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result::emailnotificationresulttype,
            resulttime = now()
        WHERE operationid = _operationid
        RETURNING alternateid INTO v_alternateid;
    END IF;

    RETURN v_alternateid; -- null => not found
END;
$$;

COMMENT ON FUNCTION notifications.updateemailnotification IS
'Updates an email notification''s result and resulttime by alternateid or by operationid.
Precedence: If both _alternateid and _operationid are non-null, only alternateid is used for lookup; _operationid may still populate the row via COALESCE.
Null return: NULL when neither identifier is provided OR no matching row exists (no update performed).
Uniqueness assumptions: alternateid is unique (primary key); operationid uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are always overwritten (no transition guarding); operationid is only set when a non-null _operationid is supplied (existing value preserved when _operationid is null).';